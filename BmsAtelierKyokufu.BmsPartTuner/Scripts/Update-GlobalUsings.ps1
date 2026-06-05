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

class FileUtils {
    static [System.Text.Encoding] GetEncoding([string]$FilePath) {
        $bytes = [System.IO.File]::ReadAllBytes($FilePath)
        $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
        return [System.Text.UTF8Encoding]::new($hasBom)
    }

    static [string] GetNewline([string]$Content) {
        if ($Content -match "`r`n") { return "`r`n" }
        if ($Content -match "`n") { return "`n" }
        return [Environment]::NewLine
    }
}

# Manages Git safety backups before executing destructive refactoring operations
class GitBackupManager {
    [string]$TargetDir

    GitBackupManager([string]$targetDir) {
        $this.TargetDir = $targetDir
    }

    # Commits all dirty working tree changes to a temporary safe commit
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

# Scans target directory and indexes namespaces and custom class/struct/enum definitions
class CsharpProject {
    [string]$TargetDir
    [string]$GlobalUsingsPath
    $CsFiles            # List[FileInfo]
    $TypeToNs           # Dictionary[string, string] (case-sensitive)
    $NsToTypes          # Dictionary[string, HashSet[string]]
    $KnownNamespaces    # HashSet[string] (case-insensitive)

    CsharpProject([string]$targetDir, [string]$globalUsingsPath) {
        $this.TargetDir = $targetDir
        $this.GlobalUsingsPath = $globalUsingsPath
        
        $this.CsFiles = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
        $this.TypeToNs = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)
        $this.NsToTypes = [System.Collections.Generic.Dictionary[string, System.Collections.Generic.HashSet[string]]]::new([System.StringComparer]::Ordinal)
        $this.KnownNamespaces = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    }

    # Performs recursive scanning on target directory
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

# Reduces fully qualified class names to short names while adding required using statements
class FqnReducer {
    $Project    # CsharpProject reference

    FqnReducer($project) {
        $this.Project = $project
    }

    # Reduces FQNs inside registered project files
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

# Performs global using promotion/demotion logic and cleans redundant using directives
class GlobalUsingsManager {
    $Project            # CsharpProject reference
    [int]$Threshold

    GlobalUsingsManager($project, [int]$threshold) {
        $this.Project = $project
        $this.Threshold = $threshold
    }

    # Core logic to audit using frequency, modify GlobalUsings.cs, and deduplicate
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

    [void]SaveGlobalUsings([System.Text.Encoding]$encoding, [System.Collections.Generic.List[string]]$globalUsingsLines) {
        $globalUsingsRaw = [System.IO.File]::ReadAllText($this.Project.GlobalUsingsPath, $encoding)
        $globalUsingsNewline = [FileUtils]::GetNewline($globalUsingsRaw)
        
        $newGlobalUsingsText = [string]::Join($globalUsingsNewline, [string[]]$globalUsingsLines.ToArray())
        [System.IO.File]::WriteAllText($this.Project.GlobalUsingsPath, $newGlobalUsingsText, $encoding)
        Write-Host "Updated GlobalUsings.cs successfully."
    }

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
