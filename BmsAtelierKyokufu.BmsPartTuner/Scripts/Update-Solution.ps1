<#
.SYNOPSIS
    ソリューションファイル (*.sln) の SolutionItems をディレクトリスキャンで自動同期します。

.DESCRIPTION
    このスクリプトは、BmsAtelierKyokufu.BmsPartTuner.sln 内の「仮想フォルダ」グループに対して、
    対応するディレクトリ配下のファイルを再スキャンし、SolutionItems を最新の状態に書き換えます。
    - コンパイルに必要な C# プロジェクト (.csproj) のエントリは一切変更しません。
    - Global セクション（ビルド構成、NestedProjects 等）も変更しません。
    - SolutionGuid・各プロジェクトの GUID は保持されます。
    - ファイルはソリューションルートからの相対パスで記述されます。

.PARAMETER SlnPath
    更新対象のソリューションファイルのパス。
    指定しない場合、スクリプトが存在するディレクトリの 2 階層上にある *.sln を自動検出します。

.EXAMPLE
    PS C:\> .\Update-Solution.ps1
    スクリプトの場所を基準にソリューションファイルを自動検出して実行します。

.EXAMPLE
    PS C:\> .\Update-Solution.ps1 -SlnPath "C:\MyProject\MyProject.sln"
    指定したソリューションファイルを対象に実行します。
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$SlnPath = $null
)

# ==============================================================================
# パス解決
# ==============================================================================

$scriptDir = $PSScriptRoot
if ([string]::IsNullOrEmpty($scriptDir)) {
    $scriptDir = Split-Path $MyInvocation.MyCommand.Path -Parent
}

if ([string]::IsNullOrEmpty($SlnPath)) {
    $slnRoot = Split-Path (Split-Path $scriptDir -Parent) -Parent
    $found = Get-ChildItem -Path $slnRoot -Filter "*.sln" -File | Select-Object -First 1
    if ($null -eq $found) {
        Write-Error "ソリューションファイルが見つかりませんでした: $slnRoot"
        exit 1
    }
    $SlnPath = $found.FullName
}

$slnRoot = Split-Path $SlnPath -Parent
Write-Host "ソリューション: $SlnPath"
Write-Host "ルートディレクトリ: $slnRoot"

# ==============================================================================
# ディレクトリスキャン設定
#
# 各エントリは以下を定義:
#   ProjectGuid : .sln 内の該当仮想フォルダの GUID
#   Directory   : スキャンするディレクトリ（ルートからの相対パス）
#   Filter      : ファイル名フィルタ（glob パターン）
#   Recurse     : サブディレクトリを再帰的にスキャンするか
# ==============================================================================
$scanConfigs = @(
    @{
        ProjectGuid = "{397BF336-22F4-490C-83CF-10C1F69F147D}"  # workflows
        Directory   = ".agent\workflows"
        Filter      = "*"
        Recurse     = $false
    },
    @{
        ProjectGuid = "{949F78AA-308F-417C-B604-C515A3E7C0FE}"  # rules
        Directory   = ".agent\rules"
        Filter      = "*"
        Recurse     = $false
    },
    @{
        ProjectGuid = "{8997F72E-9094-4A4D-9116-2F079C8DE03D}"  # math
        Directory   = "docs\adr\math"
        Filter      = "*.md"
        Recurse     = $false
    },
    @{
        ProjectGuid = "{54CB78BE-CD11-4087-9564-25F790D622C4}"  # perf
        Directory   = "docs\adr\perf"
        Filter      = "*.md"
        Recurse     = $false
    },
    @{
        ProjectGuid = "{D1097E82-557E-415C-B786-F19767EEA9D3}"  # arch
        Directory   = "docs\adr\arch"
        Filter      = "*.md"
        Recurse     = $false
    },
    @{
        ProjectGuid = "{FAEBC7A2-E9F2-406A-A8DE-4FE854A71A00}"  # teach
        Directory   = "docs\teach"
        Filter      = "*.md"
        Recurse     = $false
    },
    @{
        ProjectGuid = "{2CD22421-E81D-49CC-9003-8B35EB0D3B0E}"  # img
        Directory   = "docs\img"
        Filter      = "*"
        Recurse     = $false
    },
    @{
        ProjectGuid = "{66E82DCA-53C1-49FA-AEB0-D3A14B0D58EE}"  # javascripts
        Directory   = "docs\javascripts"
        Filter      = "*"
        Recurse     = $false
    }
)

# ==============================================================================
# スキャン設定を GUID -> ファイルリストのマップに変換
# ==============================================================================

<#*
 * 各スキャン設定に従ってディレクトリをスキャンし、GUID をキーとしたファイルリストマップを生成します。
 * @param {array} configs スキャン設定の配列
 * @param {string} slnRoot ソリューションルートディレクトリのパス
 * @returns {hashtable} GUID -> List[string] のマップ
 #>
function Build-FileMap {
    param($configs, $slnRoot)

    $map = @{}
    foreach ($cfg in $configs) {
        $absDir = Join-Path $slnRoot $cfg.Directory
        $list = [System.Collections.Generic.List[string]]::new()

        if (Test-Path $absDir) {
            $params = @{
                Path    = $absDir
                Filter  = $cfg.Filter
                File    = $true
                Recurse = $cfg.Recurse
            }
            $files = Get-ChildItem @params | Sort-Object FullName

            foreach ($f in $files) {
                $rel = $f.FullName.Substring($slnRoot.Length).TrimStart('\')
                [void]$list.Add($rel)
            }
        } else {
            Write-Warning "ディレクトリが存在しません: $absDir"
        }

        $map[$cfg.ProjectGuid] = $list
    }
    return $map
}

# ==============================================================================
# .sln テキストのパースと書き換え
# ==============================================================================

<#*
 * .sln ファイル内の Project ブロックを解析し、SolutionItems の内容を更新します。
 * C# プロジェクト (.csproj) のエントリや Global セクションは変更しません。
 * @param {string} slnText 元のソリューションファイルの全テキスト
 * @param {hashtable} fileMap GUID -> ファイルリストのマップ
 * @returns {string} 更新後のソリューションファイルテキスト
 #>
function Update-SlnText {
    param([string]$slnText, [hashtable]$fileMap)

    # 仮想フォルダの GUID
    $virtualFolderTypeGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}"

    # 各 Project...EndProject ブロックを処理するための正規表現
    $projectBlockPattern = '(?ms)^Project\("\{2150E333-8FDC-42A3-9474-1A3956D46DE8\}"\)[^\r\n]*\r?\n(.*?)^EndProject'
    $guidPattern = 'Project\("[^"]+"\)\s*=\s*"[^"]+",\s*"[^"]+",\s*"(\{[A-F0-9\-]+\})"'

    $result = [System.Text.RegularExpressions.Regex]::Replace(
        $slnText,
        $projectBlockPattern,
        {
            param($m)
            $block = $m.Value

            # このブロックの GUID を抽出
            $guidMatch = [System.Text.RegularExpressions.Regex]::Match($block, $guidPattern, 'IgnoreCase')
            if (-not $guidMatch.Success) {
                return $block
            }
            $guid = $guidMatch.Groups[1].Value.ToUpper()

            # このGUIDに対するファイルリストがあるか確認
            $upperMap = @{}
            foreach ($k in $fileMap.Keys) { $upperMap[$k.ToUpper()] = $fileMap[$k] }

            if (-not $upperMap.ContainsKey($guid)) {
                return $block
            }

            $files = $upperMap[$guid]

            # ヘッダー行（Project(...)行）を取得
            $headerLine = ($block -split '\r?\n')[0]
            $newline = if ($block -match '\r\n') { "`r`n" } else { "`n" }

            # SolutionItems セクションを構築
            $sb = [System.Text.StringBuilder]::new()
            [void]$sb.Append($headerLine + $newline)

            if ($files.Count -gt 0) {
                [void]$sb.Append("`tProjectSection(SolutionItems) = preProject" + $newline)
                foreach ($f in $files) {
                    [void]$sb.Append("`t`t$f = $f" + $newline)
                }
                [void]$sb.Append("`tEndProjectSection" + $newline)
            }

            [void]$sb.Append("EndProject")
            return $sb.ToString()
        },
        [System.Text.RegularExpressions.RegexOptions]::Multiline
    )

    return $result
}

# ==============================================================================
# メイン処理
# ==============================================================================

$encoding = [System.Text.UTF8Encoding]::new($false)  # BOM なし UTF-8
$slnText = [System.IO.File]::ReadAllText($SlnPath, $encoding)
$newline = if ($slnText -match '\r\n') { "`r`n" } else { "`n" }

$fileMap = Build-FileMap -configs $scanConfigs -slnRoot $slnRoot
$newText = Update-SlnText -slnText $slnText -fileMap $fileMap

if ($newText -eq $slnText) {
    Write-Host "変更なし。ソリューションファイルは最新です。"
} else {
    # BOM の有無を元ファイルから継承
    $bytes = [System.IO.File]::ReadAllBytes($SlnPath)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $writeEncoding = [System.Text.UTF8Encoding]::new($hasBom)

    [System.IO.File]::WriteAllText($SlnPath, $newText, $writeEncoding)
    Write-Host "ソリューションファイルを更新しました: $SlnPath"
}
