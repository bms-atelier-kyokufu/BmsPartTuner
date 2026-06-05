<#
.SYNOPSIS
    C# プロジェクト内の using 宣言および完全修飾名 (FQN) を整理し、頻出する名前空間を GlobalUsings.cs に昇格します。

.DESCRIPTION
    このスクリプトは、指定されたディレクトリ以下の C# ファイル (*.cs) をスキャンし、以下の処理を実行します。
    1. Git のクリーン状態を確認し、未コミットの変更がある場合は自動的に退避用のコミット (wip: auto-saved...) を作成します。
    2. プロジェクト内で定義されている独自の型と名前空間、およびプロジェクト内の FQN と using 宣言を解析します。
    3. 指定された閾値 (PromotionThreshold) 以上のファイルで使われている名前空間を GlobalUsings.cs にまとめ、グローバル using として定義します。
    4. 各 C# ファイルから、GlobalUsings.cs に移行した using 宣言を削除します。
    5. 各 C# ファイル内の FQN (例: System.IO.File) を、名前空間を省略した形式 (例: File) に置換・削減します。
    6. UTF-8 BOM を適切に検出・維持してファイルの読み書きを行い、文字化けを防ぎます。

.PARAMETER TargetDir
    スキャンおよびクリーンアップ対象となるプロジェクトのルートディレクトリパス。
    指定しない場合、スクリプトが存在するディレクトリの親ディレクトリが自動的に使用されます。

.PARAMETER GlobalUsingsPath
    GlobalUsings.cs を作成・更新するファイルのフルパス。
    指定しない場合、対象ディレクトリ内で最初に見つかった C# プロジェクトディレクトリ配下に 'GlobalUsings.cs' として配置されます。

.PARAMETER PromotionThreshold
    名前空間をグローバル using に昇格させるための閾値（出現ファイル数）。デフォルトは 3 です。
    例えば 3 を指定した場合、3 つ以上のファイルで using もしくは FQN として現れる名前空間が GlobalUsings.cs に昇格されます。

.EXAMPLE
    PS C:\> .\Update-GlobalUsings.ps1
    デフォルトのディレクトリに対して、閾値 3 で実行します。

.EXAMPLE
    PS C:\> .\Update-GlobalUsings.ps1 -TargetDir "C:\MyProject" -PromotionThreshold 5
    "C:\MyProject" ディレクトリ配下を対象に、5 つ以上のファイルで使われている名前空間をグローバル using に昇格させます。
#>
[CmdletBinding()]
param (
    [Parameter(Position = 0)]
    [string]$TargetDir = $null,

    [Parameter(Position = 1)]
    [string]$GlobalUsingsPath = $null,

    [Parameter(Position = 2)]
    [int]$PromotionThreshold = 3
)

# ==============================================================================
# Helper class definitions for C# global using and FQN refactoring
# ==============================================================================

<#*
 * ファイルの読み書きやエンコーディングの判定を行うためのユーティリティクラス。
 #>
class FileUtils {
    <#*
     * 指定されたファイルの文字エンコーディング（UTF-8 BOMの有無）を自動判定します。
     * @param {string} FilePath 対象ファイルのパス
     * @returns {System.Text.Encoding} UTF-8（BOMの有無に応じたインスタンス）
     #>
    static [System.Text.Encoding] GetEncoding([string]$FilePath) {
        $bytes = [System.IO.File]::ReadAllBytes($FilePath)
        $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
        return [System.Text.UTF8Encoding]::new($hasBom)
    }

    <#*
     * 指定されたテキスト内の改行コード（CRLFまたはLF）を検出し、最適な改行文字を返します。
     * @param {string} Content 対象テキスト
     * @returns {string} 改行文字列（\r\n または \n）
     #>
    static [string] GetNewline([string]$Content) {
        if ($Content -match "`r`n") { return "`r`n" }
        if ($Content -match "`n") { return "`n" }
        return [Environment]::NewLine
    }
}

<#*
 * 破壊的なリファクタリング操作を実行する前に、Gitの状態を確認しバックアップコミットを作成するマネージャー。
 #>
class GitBackupManager {
    [string]$TargetDir

    <#*
     * コンストラクタ
     * @param {string} targetDir 対象ディレクトリのパス
     #>
    GitBackupManager([string]$targetDir) {
        $this.TargetDir = $targetDir
    }

    <#*
     * 未コミットの変更がある場合に、自動的に退避用のコミット（wip: auto-saved...）を作成します。
     * @returns {void}
     #>
    [void]CreateSafetyCommit() {
        Write-Host "Checking Git status..."
        $gitStatus = git -C $this.TargetDir status --porcelain 2>$null
        if ($LASTEXITCODE -eq 0 -and $gitStatus) {
            Write-Host "Uncommitted changes detected. Creating temporary safety commit..."
            git -C $this.TargetDir add -A
            $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
            git -C $this.TargetDir commit -m "wip: auto-saved before global using update $timestamp"
            Write-Host "Git state saved."
        } elseif ($LASTEXITCODE -ne 0) {
            Write-Host "Warning: Git command failed or directory is not a git repository. Skipping auto-save."
        } else {
            Write-Host "Git directory is clean. No safety commit needed."
        }
    }
}

<#*
 * 対象ディレクトリ内の C# プロジェクトファイルをスキャンし、定義されている独自の型や名前空間をインデックス化するクラス。
 #>
class CsharpProject {
    [string]$TargetDir
    [string]$GlobalUsingsPath
    $CsFiles            # List[FileInfo]
    $TypeToNs           # Dictionary[string, string] (case-sensitive)
    $NsToTypes          # Dictionary[string, HashSet[string]]
    $KnownNamespaces    # HashSet[string] (case-insensitive)

    <#*
     * コンストラクタ
     * @param {string} targetDir 対象ディレクトリのパス
     * @param {string} globalUsingsPath GlobalUsings.csのファイルパス
     #>
    CsharpProject([string]$targetDir, [string]$globalUsingsPath) {
        $this.TargetDir = $targetDir
        $this.GlobalUsingsPath = $globalUsingsPath
        
        $this.CsFiles = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
        $this.TypeToNs = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)
        $this.NsToTypes = [System.Collections.Generic.Dictionary[string, System.Collections.Generic.HashSet[string]]]::new([System.StringComparer]::Ordinal)
        $this.KnownNamespaces = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    }

    <#*
     * 対象ディレクトリ配下の C# ファイルを再帰的にスキャンし、名前空間や定義されているクラス・構造体・インターフェース名などを解析します。
     * @returns {void}
     #>
    [void]Scan() {
        $rawFiles = Get-ChildItem -Path $this.TargetDir -Filter *.cs -Recurse
        if ($null -eq $rawFiles) {
            return
        }

        # Populate CsFiles, ignoring build outputs, auto-generated code, and the scripts directory
        foreach ($file in $rawFiles) {
            if ($null -eq $file -or $file.PSIsContainer -or [string]::IsNullOrEmpty($file.FullName)) {
                continue
            }
            if ($file.FullName -match '\\(bin|obj|Generated|Scripts)\\') {
                continue
            }
            if ($file.FullName -eq $this.GlobalUsingsPath) {
                continue
            }
            [void]$this.CsFiles.Add($file)
        }

        # Parse namespaces and defined class/struct/interface names
        foreach ($file in $this.CsFiles) {
            $encoding = [FileUtils]::GetEncoding($file.FullName)
            $content = [System.IO.File]::ReadAllText($file.FullName, $encoding)
            
            # Identify namespace
            $fileNamespace = $null
            if ($content -match '(?m)^\s*namespace\s+([A-Za-z0-9_\.]+);?') {
                $fileNamespace = $Matches[1].Trim().TrimEnd(';')
                [void]$this.KnownNamespaces.Add($fileNamespace)
            }

            if ([string]::IsNullOrEmpty($fileNamespace)) {
                continue
            }

            # Map all types defined in the namespace
            $typeMatches = [regex]::Matches($content, '\b(?:class|struct|interface|enum|record)\s+([A-Za-z0-9_]+)\b')
            foreach ($match in $typeMatches) {
                $typeName = $match.Groups[1].Value
                if (-not $this.TypeToNs.ContainsKey($typeName)) {
                    $this.TypeToNs[$typeName] = $fileNamespace
                }
                if (-not $this.NsToTypes.ContainsKey($fileNamespace)) {
                    $this.NsToTypes[$fileNamespace] = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
                }
                [void]$this.NsToTypes[$fileNamespace].Add($typeName)
            }
        }
    }
}

<#*
 * C#コード内の完全修飾名（FQN）を短い型名に削減し、必要な using 宣言を追加するクラス。
 #>
class FqnReducer {
    $Project    # CsharpProject reference

    <#*
     * コンストラクタ
     * @param {CsharpProject} project C#プロジェクトの解析情報
     #>
    FqnReducer($project) {
        $this.Project = $project
    }

    <#*
     * プロジェクト内のファイルに対してFQNの削減処理を行い、変更されたファイルのリストを返します。
     * @returns {System.Collections.Generic.List[System.IO.FileInfo]} 変更があったファイルのリスト
     #>
    [System.Collections.Generic.List[System.IO.FileInfo]]ReduceFqns() {
        $modifiedFiles = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
        
        # Match only FQNs belonging to our own project prefix
        $fqnPattern = '(?<!namespace\s+)(?<!using\s+)(?<!global\s+using\s+)\b(BmsAtelierKyokufu\.BmsPartTuner\.(?:[A-Za-z0-9_]+\.)+)([A-Za-z0-9_]+)\b'
        $fqnRegex = [regex]::new($fqnPattern)

        foreach ($file in $this.Project.CsFiles) {
            $this.ProcessFile($file, $fqnRegex, $modifiedFiles)
        }
        return $modifiedFiles
    }

    <#*
     * 単一のファイルに対してFQNの置換と必要な using 宣言の挿入を行います。
     * @param {System.IO.FileInfo} file 対象のファイルオブジェクト
     * @param {regex} fqnRegex FQN検出用の正規表現
     * @param {System.Collections.Generic.List[System.IO.FileInfo]} modifiedFiles 変更されたファイルの追跡リスト
     * @returns {void}
     #>
    [void]ProcessFile([System.IO.FileInfo]$file, [regex]$fqnRegex, [System.Collections.Generic.List[System.IO.FileInfo]]$modifiedFiles) {
        $encoding = [FileUtils]::GetEncoding($file.FullName)
        $content = [System.IO.File]::ReadAllText($file.FullName, $encoding)
        $originalContent = $content

        $usingsToAdd = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        $content = $this.ReplaceFqns($content, $fqnRegex, $usingsToAdd)

        if ($usingsToAdd.Count -gt 0) {
            $this.InsertUsingsAndSave($file, $content, $originalContent, $encoding, $usingsToAdd)
            Write-Host "Reduced FQN in: $($file.FullName)"
            [void]$modifiedFiles.Add($file)
        }
    }

    <#*
     * テキスト内のFQNを検出し、短い型名に置換します。また、追加すべき名前空間を収集します。
     * @param {string} content ファイルの全テキスト内容
     * @param {regex} fqnRegex FQN検出用の正規表現
     * @param {System.Collections.Generic.HashSet[string]} usingsToAdd 追加すべき名前空間を格納するセット
     * @returns {string} FQN置換後のテキスト
     #>
    [string]ReplaceFqns([string]$content, [regex]$fqnRegex, [System.Collections.Generic.HashSet[string]]$usingsToAdd) {
        $matches = $fqnRegex.Matches($content)
        
        for ($i = $matches.Count - 1; $i -ge 0; $i--) {
            $m = $matches[$i]
            $fullNs = $m.Groups[1].Value.TrimEnd('.')
            $typeName = $m.Groups[2].Value

            $lineStart = 0
            if ($m.Index -gt 0) {
                $lastNewline = $content.LastIndexOf("`n", $m.Index - 1)
                if ($lastNewline -ge 0) {
                    $lineStart = $lastNewline + 1
                }
            }
            $lineEnd = $content.IndexOf("`n", $m.Index)
            if ($lineEnd -lt 0) { $lineEnd = $content.Length }
            $lineText = $content.Substring($lineStart, $lineEnd - $lineStart)

            if ($lineText -match '^\s*(global\s+)?using\s+[A-Za-z0-9_]+\s*=') {
                continue
            }

            if ($this.Project.KnownNamespaces.Contains($fullNs) -and $this.Project.NsToTypes.ContainsKey($fullNs) -and $this.Project.NsToTypes[$fullNs].Contains($typeName)) {
                $content = $content.Substring(0, $m.Index) + $typeName + $content.Substring($m.Index + $m.Length)
                [void]$usingsToAdd.Add($fullNs)
            }
        }
        return $content
    }

    <#*
     * 置換後のテキストに不足している using 宣言を挿入し、適切なエンコーディングでファイルに保存します。
     * @param {System.IO.FileInfo} file 対象のファイルオブジェクト
     * @param {string} content 置換後のファイル内容
     * @param {string} originalContent 置換前のファイル内容（改行コード判定用）
     * @param {System.Text.Encoding} encoding ファイルの保存エンコーディング
     * @param {System.Collections.Generic.HashSet[string]} usingsToAdd 追加すべき名前空間のセット
     * @returns {void}
     #>
    [void]InsertUsingsAndSave([System.IO.FileInfo]$file, [string]$content, [string]$originalContent, [System.Text.Encoding]$encoding, [System.Collections.Generic.HashSet[string]]$usingsToAdd) {
        $lines = [System.Collections.Generic.List[string]]::new()
        $rawLines = $content -split '\r?\n'
        [void]$lines.AddRange($rawLines)

        $existingUsings = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        $lastUsingIndex = -1
        $namespaceIndex = -1
        
        for ($j = 0; $j -lt $lines.Count; $j++) {
            $line = $lines[$j]
            if ($line -match '^\s*using\s+([^;]+);') {
                [void]$existingUsings.Add($Matches[1].Trim())
                $lastUsingIndex = $j
            } elseif ($line -match '^\s*namespace\s+') {
                $namespaceIndex = $j
            }
        }

        $insertedCount = 0
        foreach ($ns in $usingsToAdd) {
            if (-not $existingUsings.Contains($ns)) {
                $usingLine = "using $ns;"
                if ($lastUsingIndex -ge 0) {
                    $lines.Insert($lastUsingIndex + 1 + $insertedCount, $usingLine)
                    $insertedCount++
                } elseif ($namespaceIndex -ge 0) {
                    $lines.Insert($namespaceIndex + $insertedCount, $usingLine)
                    $insertedCount++
                } else {
                    $lines.Insert($insertedCount, $usingLine)
                    $insertedCount++
                }
            }
        }

        $newline = [FileUtils]::GetNewline($originalContent)
        $newText = [string]::Join($newline, [string[]]$lines.ToArray())
        $newText = $newText -replace "^(\r?\n)+", ""

        [System.IO.File]::WriteAllText($file.FullName, $newText, $encoding)
    }
}

<#*
 * グローバル using の昇格・降格ロジックを制御し、重複する using 宣言のクリーンアップを行うクラス。
 #>
class GlobalUsingsManager {
    $Project            # CsharpProject reference
    [int]$Threshold

    <#*
     * コンストラクタ
     * @param {CsharpProject} project C#プロジェクトの解析情報
     * @param {int} threshold グローバル using への昇格に必要なファイル数の閾値
     #>
    GlobalUsingsManager($project, [int]$threshold) {
        $this.Project = $project
        $this.Threshold = $threshold
    }

    <#*
     * グローバル using の昇格・降格、GlobalUsings.cs の書き換え、および重複する通常の using 宣言のクリーンアップを実行します。
     * @param {System.Collections.Generic.List[System.IO.FileInfo]} modifiedFiles FQN削減処理で既に変更されたファイルのリスト
     * @returns {void}
     #>
    [void]UpdateGlobalUsings([System.Collections.Generic.List[System.IO.FileInfo]]$modifiedFiles) {
        $usingCounts = $this.CountInternalUsings()
        
        $globalUsings = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        $globalUsingsEncoding = [FileUtils]::GetEncoding($this.Project.GlobalUsingsPath)
        $globalUsingsLines = $this.ParseGlobalUsings($globalUsingsEncoding, $globalUsings)

        $promotedAny = $this.PromoteFrequentUsings($usingCounts, $globalUsings, $globalUsingsLines)
        $demotedAny = $this.DemoteUnusedGlobalUsings($globalUsings, $globalUsingsLines)

        if ($promotedAny -or $demotedAny) {
            $this.SaveGlobalUsings($globalUsingsEncoding, $globalUsingsLines)
        }

        $this.CleanRedundantUsings($globalUsings, $modifiedFiles)
    }

    <#*
     * プロジェクト内の各ファイルで使用されている名前空間の出現頻度をカウントします。
     * @returns {System.Collections.Generic.Dictionary[string, int]} 名前空間と出現回数のマッピング
     #>
    [System.Collections.Generic.Dictionary[string, int]]CountInternalUsings() {
        Write-Host "Analyzing using statements usage frequency for global promotion..."
        $usingCounts = [System.Collections.Generic.Dictionary[string, int]]::new([System.StringComparer]::Ordinal)

        foreach ($file in $this.Project.CsFiles) {
            $encoding = [FileUtils]::GetEncoding($file.FullName)
            $lines = [System.IO.File]::ReadAllLines($file.FullName, $encoding)
            foreach ($line in $lines) {
                if ($line -match '^\s*using\s+([^;]+);') {
                    $ns = $Matches[1].Trim()
                    if ($this.Project.KnownNamespaces.Contains($ns)) {
                        if (-not $usingCounts.ContainsKey($ns)) {
                            $usingCounts[$ns] = 0
                        }
                        $usingCounts[$ns]++
                    }
                }
            }
        }
        return $usingCounts
    }

    <#*
     * 既存の GlobalUsings.cs ファイルからグローバル using 宣言を解析して読み込みます。
     * @param {System.Text.Encoding} encoding ファイルのエンコーディング
     * @param {System.Collections.Generic.HashSet[string]} globalUsings 解析した名前空間を追加するセット
     * @returns {System.Collections.Generic.List[string]} 行ごとの全テキスト内容
     #>
    [System.Collections.Generic.List[string]]ParseGlobalUsings([System.Text.Encoding]$encoding, [System.Collections.Generic.HashSet[string]]$globalUsings) {
        $lines = [System.Collections.Generic.List[string]]::new()
        [void]$lines.AddRange([System.IO.File]::ReadAllLines($this.Project.GlobalUsingsPath, $encoding))

        foreach ($line in $lines) {
            if ($line -match '^\s*global\s+using\s+([^;]+);') {
                $ns = $Matches[1].Trim()
                [void]$globalUsings.Add($ns)
            }
        }
        return $lines
    }

    <#*
     * 出現回数が閾値以上の名前空間をグローバル using に昇格し、行リストに追加します。
     * @param {System.Collections.Generic.Dictionary[string, int]} usingCounts 名前空間の出現頻度
     * @param {System.Collections.Generic.HashSet[string]} globalUsings 現在のグローバル using の名前空間セット
     * @param {System.Collections.Generic.List[string]} globalUsingsLines GlobalUsings.cs の行リスト
     * @returns {bool} 昇格が行われた場合は $true
     #>
    [bool]PromoteFrequentUsings([System.Collections.Generic.Dictionary[string, int]]$usingCounts, [System.Collections.Generic.HashSet[string]]$globalUsings, [System.Collections.Generic.List[string]]$globalUsingsLines) {
        $promotedAny = $false
        foreach ($ns in $usingCounts.Keys) {
            $count = $usingCounts[$ns]
            if ($count -ge $this.Threshold -and -not $globalUsings.Contains($ns)) {
                Write-Host "Promoting namespace to GlobalUsings: $ns (Used in $count files)"
                
                $insertIndex = -1
                for ($k = 0; $k -lt $globalUsingsLines.Count; $k++) {
                    $line = $globalUsingsLines[$k]
                    if ($line -match '^\s*using\s+' -or $line -match '^\s*\[assembly:') {
                        $insertIndex = $k
                        break
                    }
                }
                
                $globalUsingLine = "global using $ns;"
                if ($insertIndex -ge 0) {
                    $globalUsingsLines.Insert($insertIndex, $globalUsingLine)
                } else {
                    [void]$globalUsingsLines.Add($globalUsingLine)
                }
                [void]$globalUsings.Add($ns)
                $promotedAny = $true
            }
        }
        return $promotedAny
    }

    <#*
     * プロジェクト内で一度も使用されなくなったグローバル using を検出し、降格（削除）します。
     * @param {System.Collections.Generic.HashSet[string]} globalUsings 現在のグローバル using の名前空間セット
     * @param {System.Collections.Generic.List[string]} globalUsingsLines GlobalUsings.cs の行リスト
     * @returns {bool} 降格が行われた場合は $true
     #>
    [bool]DemoteUnusedGlobalUsings([System.Collections.Generic.HashSet[string]]$globalUsings, [System.Collections.Generic.List[string]]$globalUsingsLines) {
        Write-Host "Scanning for unused global usings..."
        $demotedAny = $false
        $usingsToDemote = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

        foreach ($ns in $globalUsings) {
            if (-not $this.Project.KnownNamespaces.Contains($ns)) {
                continue
            }

            if ($this.Project.NsToTypes.ContainsKey($ns)) {
                $typesInNs = $this.Project.NsToTypes[$ns]
                $typeUsed = $false
                
                foreach ($file in $this.Project.CsFiles) {
                    $encoding = [FileUtils]::GetEncoding($file.FullName)
                    $fileText = [System.IO.File]::ReadAllText($file.FullName, $encoding)

                    foreach ($t in $typesInNs) {
                        if ($fileText -match "\b$t\b") {
                            $typeUsed = $true
                            break
                        }
                    }
                    if ($typeUsed) { break }
                }

                if (-not $typeUsed) {
                    Write-Host "Demoting unused global using: $ns"
                    [void]$usingsToDemote.Add($ns)
                    $demotedAny = $true
                }
            }
        }

        if ($demotedAny) {
            for ($k = $globalUsingsLines.Count - 1; $k -ge 0; $k--) {
                $line = $globalUsingsLines[$k]
                if ($line -match '^\s*global\s+using\s+([^;]+);') {
                    $ns = $Matches[1].Trim()
                    if ($usingsToDemote.Contains($ns)) {
                        $globalUsingsLines.RemoveAt($k)
                        [void]$globalUsings.Remove($ns)
                    }
                }
            }
        }
        return $demotedAny
    }

    <#*
     * GlobalUsings.cs の行リストをヘッダー部分、フッター部分、およびグローバル using 宣言の部分に分割します。
     * @param {System.Collections.Generic.List[string]} globalUsingsLines 全行リスト
     * @returns {hashtable} HeaderLines, FooterLines, GlobalUsings を含むハッシュテーブル
     #>
    [hashtable]ParseGlobalUsingSections([System.Collections.Generic.List[string]]$globalUsingsLines) {
        $headerLines = [System.Collections.Generic.List[string]]::new()
        $footerLines = [System.Collections.Generic.List[string]]::new()
        $globalUsings = [System.Collections.Generic.List[string]]::new()

        $firstGlobalUsingIndex = -1
        for ($i = 0; $i -lt $globalUsingsLines.Count; $i++) {
            if ($globalUsingsLines[$i] -match '^\s*global\s+using\s+') {
                $firstGlobalUsingIndex = $i
                break
            }
        }

        if ($firstGlobalUsingIndex -eq -1) {
            $footerLines.AddRange($globalUsingsLines)
        } else {
            for ($i = 0; $i -lt $firstGlobalUsingIndex; $i++) {
                [void]$headerLines.Add($globalUsingsLines[$i])
            }
            for ($i = $firstGlobalUsingIndex; $i -lt $globalUsingsLines.Count; $i++) {
                $line = $globalUsingsLines[$i]
                if ($line -match '^\s*global\s+using\s+([^;]+);') {
                    $ns = $Matches[1].Trim()
                    [void]$globalUsings.Add($ns)
                } else {
                    if ($line.Trim() -ne "") {
                        [void]$footerLines.Add($line)
                    }
                }
            }
        }

        return @{
            HeaderLines = $headerLines
            FooterLines = $footerLines
            GlobalUsings = $globalUsings
        }
    }

    <#*
     * グローバル using 宣言をルート名前空間（例：System、BmsAtelierKyokufu など）ごとにグループ化します。
     * @param {System.Collections.Generic.List[string]} globalUsings グローバル using のリスト
     * @returns {System.Collections.Generic.Dictionary[string, System.Collections.Generic.List[string]]} ルート名前空間と行リストのマッピング
     #>
    [System.Collections.Generic.Dictionary[string, System.Collections.Generic.List[string]]]GroupGlobalUsings([System.Collections.Generic.List[string]]$globalUsings) {
        $groups = [System.Collections.Generic.Dictionary[string, System.Collections.Generic.List[string]]]::new([System.StringComparer]::OrdinalIgnoreCase)

        foreach ($ns in $globalUsings) {
            $cleanNs = $ns -replace '^\s*static\s+', ''
            if ($cleanNs -match '=\s*(.+)$') {
                $cleanNs = $Matches[1].Trim()
            }
            $root = ($cleanNs -split '\.')[0].Trim()

            if (-not $groups.ContainsKey($root)) {
                $groups[$root] = [System.Collections.Generic.List[string]]::new()
            }
            [void]$groups[$root].Add("global using $ns;")
        }
        return $groups
    }

    <#*
     * ヘッダー、グループ化・ソートされたグローバル using、およびフッターを整形された行リストとして組み立てます。
     * @param {System.Collections.Generic.List[string]} headerLines ヘッダー行
     * @param {System.Collections.Generic.Dictionary[string, System.Collections.Generic.List[string]]} groups グループ化された using
     * @param {System.Collections.Generic.List[string]} footerLines フッター行
     * @returns {System.Collections.Generic.List[string]} 整形された全行リスト
     #>
    [System.Collections.Generic.List[string]]FormatGlobalUsingsLines([System.Collections.Generic.List[string]]$headerLines, [System.Collections.Generic.Dictionary[string, System.Collections.Generic.List[string]]]$groups, [System.Collections.Generic.List[string]]$footerLines) {
        $sortedKeys = [System.Collections.Generic.List[string]]::new($groups.Keys)
        $sortedKeys.Sort([System.StringComparer]::OrdinalIgnoreCase)

        $newLines = [System.Collections.Generic.List[string]]::new()
        
        while ($headerLines.Count -gt 0 -and $headerLines[$headerLines.Count - 1].Trim() -eq "") {
            $headerLines.RemoveAt($headerLines.Count - 1)
        }
        [void]$newLines.AddRange($headerLines)
        if ($newLines.Count -gt 0 -and $sortedKeys.Count -gt 0) {
            [void]$newLines.Add("")
        }

        for ($i = 0; $i -lt $sortedKeys.Count; $i++) {
            $key = $sortedKeys[$i]
            $groupList = $groups[$key]
            $groupList.Sort([System.StringComparer]::OrdinalIgnoreCase)
            
            [void]$newLines.AddRange($groupList)
            
            if ($i -lt $sortedKeys.Count - 1) {
                [void]$newLines.Add("")
            }
        }

        if ($footerLines.Count -gt 0) {
            if ($newLines.Count -gt 0) {
                [void]$newLines.Add("")
            }
            [void]$newLines.AddRange($footerLines)
        }
        return $newLines
    }

    <#*
     * 昇格・降格が適用されたグローバル using リストをグループ化・ソートして GlobalUsings.cs に保存します。
     * @param {System.Text.Encoding} encoding ファイルのエンコーディング
     * @param {System.Collections.Generic.List[string]} globalUsingsLines 更新された行リスト
     * @returns {void}
     #>
    [void]SaveGlobalUsings([System.Text.Encoding]$encoding, [System.Collections.Generic.List[string]]$globalUsingsLines) {
        $sections = $this.ParseGlobalUsingSections($globalUsingsLines)
        $groups = $this.GroupGlobalUsings($sections.GlobalUsings)
        $newLines = $this.FormatGlobalUsingsLines($sections.HeaderLines, $groups, $sections.FooterLines)

        $globalUsingsRaw = [System.IO.File]::ReadAllText($this.Project.GlobalUsingsPath, $encoding)
        $globalUsingsNewline = [FileUtils]::GetNewline($globalUsingsRaw)
        
        $newGlobalUsingsText = [string]::Join($globalUsingsNewline, [string[]]$newLines.ToArray())
        $newGlobalUsingsText = $newGlobalUsingsText.TrimEnd() + $globalUsingsNewline

        [System.IO.File]::WriteAllText($this.Project.GlobalUsingsPath, $newGlobalUsingsText, $encoding)
        Write-Host "Updated, sorted, and grouped GlobalUsings.cs successfully."
    }

    <#*
     * グローバル using に昇格されたため冗長となった、各 C# ファイル内の通常の using 宣言を削除します。
     * @param {System.Collections.Generic.HashSet[string]} globalUsings 有効なグローバル using のセット
     * @param {System.Collections.Generic.List[System.IO.FileInfo]} modifiedFiles 変更ファイルリスト
     * @returns {void}
     #>
    [void]CleanRedundantUsings([System.Collections.Generic.HashSet[string]]$globalUsings, [System.Collections.Generic.List[System.IO.FileInfo]]$modifiedFiles) {
        Write-Host "Removing redundant normal using statements..."
        $totalRemoved = 0
        $modifiedFilesCount = 0

        foreach ($file in $this.Project.CsFiles) {
            $encoding = [FileUtils]::GetEncoding($file.FullName)
            $lines = [System.IO.File]::ReadAllLines($file.FullName, $encoding)
            $newLines = [System.Collections.Generic.List[string]]::new()
            $fileRemovedCount = 0

            foreach ($line in $lines) {
                if ($line -match '^\s*using\s+([^;]+);') {
                    $usingContent = $Matches[1].Trim()
                    if ($globalUsings.Contains($usingContent)) {
                        $fileRemovedCount++
                        $totalRemoved++
                        continue
                    }
                }
                [void]$newLines.Add($line)
            }

            if ($fileRemovedCount -gt 0 -or $modifiedFiles.Contains($file)) {
                $rawText = [System.IO.File]::ReadAllText($file.FullName, $encoding)
                $newline = [FileUtils]::GetNewline($rawText)

                $newText = [string]::Join($newline, [string[]]$newLines.ToArray())
                $newText = $newText -replace "^(\r?\n)+", ""

                [System.IO.File]::WriteAllText($file.FullName, $newText, $encoding)
                Write-Host "Cleaned: $($file.FullName) (Removed $fileRemovedCount redundant using(s))"
                $modifiedFilesCount++
            }
        }

        Write-Host "Finished. Cleaned $modifiedFilesCount file(s), removed $totalRemoved global using duplicates in total."
    }
}

# ==============================================================================
# Script Execution Entrypoint
# ==============================================================================

try {
    $ErrorActionPreference = "Stop"

    # Dynamic target path resolution
    $scriptDir = $PSScriptRoot
    if ([string]::IsNullOrEmpty($scriptDir)) {
        $scriptDir = Split-Path $MyInvocation.MyCommand.Path -Parent
    }
    if ([string]::IsNullOrEmpty($TargetDir)) {
        $TargetDir = Split-Path $scriptDir -Parent
    }
    if ([string]::IsNullOrEmpty($GlobalUsingsPath)) {
        $GlobalUsingsPath = Join-Path $TargetDir "GlobalUsings.cs"
    }

    Write-Host "Target Directory: $TargetDir"
    Write-Host "Global Usings File: $GlobalUsingsPath"
    Write-Host "Promotion Threshold: $PromotionThreshold"

    # 1. Execute Git safety commit backup
    $gitManager = [GitBackupManager]::new($TargetDir)
    $gitManager.CreateSafetyCommit()

    # 2. Analyze C# project directories
    $project = [CsharpProject]::new($TargetDir, $GlobalUsingsPath)
    $project.Scan()

    # 3. Perform FQN to using reduction
    $reducer = [FqnReducer]::new($project)
    $modifiedFiles = $reducer.ReduceFqns()

    # 4. Promoted frequent usings and clean redundant usings
    $usingsManager = [GlobalUsingsManager]::new($project, $PromotionThreshold)
    $usingsManager.UpdateGlobalUsings($modifiedFiles)

    Write-Host "All tasks completed successfully."
} catch {
    Write-Host "An error occurred during execution."
    Write-Host "Error message: $_"
    Write-Host "Stack Trace:"
    Write-Host $_.ScriptStackTrace
    Write-Host "Exception details: $($_.Exception.ToString())"
    exit 1
}
