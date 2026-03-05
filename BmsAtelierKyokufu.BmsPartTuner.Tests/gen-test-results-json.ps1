# ---------------------------------------------------------
# BmsPartTuner Test Runner & AI Context Generator
# 注意: カレントディレクトリを取得するために、スクリプトはテストプロジェクトルートで実行すること
# ---------------------------------------------------------

# 1. テストの実施（カバレッジ収集を有効化）
Write-Host "Running tests with code coverage..." -ForegroundColor Cyan
# --collect:"XPlat Code Coverage" で Cobertura 形式の XML を出力
dotnet test --logger "trx" --collect:"XPlat Code Coverage"

# 2. パスの設定
$baseDir = Get-Location
$resultsDir = Join-Path $baseDir "TestResults"
$outputJson = Join-Path $resultsDir "ai_context.json"

# 結果フォルダがなければ中断
if (-not (Test-Path $resultsDir)) {
    Write-Error "TestResults directory not found."
    return
}

# 3. 古いTRXファイルの掃除（最新の1件だけを残して削除）
Write-Host "Cleaning old test results..." -ForegroundColor Cyan
$oldFiles = Get-ChildItem -Path $resultsDir -Filter *.trx | 
            Sort-Object LastWriteTime -Descending | 
            Select-Object -Skip 1
    
if ($oldFiles) {
    $oldFiles | Remove-Item -Force
    Write-Host ("Removed {0} old .trx file(s)." -f $oldFiles.Count) -ForegroundColor Yellow
}

# 4. 最新のTRXファイルを取得（テスト結果用）
$trxFile = Get-ChildItem -Path $resultsDir -Filter *.trx | 
            Sort-Object LastWriteTime -Descending | 
            Select-Object -First 1

if (-not $trxFile) {
    Write-Error "No .trx file found."
    return
}

# 5. 最新のカバレッジファイルを取得（カバレッジ用）
# coverlet は TestResults/{guid}/coverage.cobertura.xml に出力する
$coverageFile = Get-ChildItem -Path $resultsDir -Recurse -Filter "coverage.cobertura.xml" | 
                Sort-Object LastWriteTime -Descending | 
                Select-Object -First 1

# ---------------------------------------------------------
# データ抽出処理
# ---------------------------------------------------------

Write-Host "Processing results from: $($trxFile.Name)" -ForegroundColor Cyan

# A. 失敗したテストの抽出
[xml]$xml = Get-Content $trxFile.FullName
$failedTests = $xml.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq 'Failed' } | ForEach-Object {
    [PSCustomObject]@{
        TestName     = $_.testName
        Outcome      = $_.outcome
        ErrorMessage = $_.Output.ErrorInfo.Message.Trim()
        StackTrace   = $_.Output.ErrorInfo.StackTrace
    }
}

# B. コードカバレッジ率の詳細抽出
$coverageSummary = @{
    LineRate   = "N/A"
    BranchRate = "N/A"
}
$coverageDetails = @()

if ($coverageFile) {
    Write-Host "Parsing coverage details..." -ForegroundColor Cyan
    [xml]$covXml = Get-Content $coverageFile.FullName
    
    # 全体サマリー (0.0~1.0 を %表記に)
    $lineRate = [Math]::Round([double]$covXml.coverage.'line-rate' * 100, 1)
    $branchRate = [Math]::Round([double]$covXml.coverage.'branch-rate' * 100, 1)
    $coverageSummary.LineRate = "$lineRate%"
    $coverageSummary.BranchRate = "$branchRate%"
    
    # クラスごとの詳細抽出
    # Cobertura XML構造: coverage > packages > package > classes > class
    $classes = $covXml.SelectNodes("//class")
    
    foreach ($class in $classes) {
        $cLineRate = [Math]::Round([double]$class.'line-rate' * 100, 1)
        $cBranchRate = [Math]::Round([double]$class.'branch-rate' * 100, 1)
        
        # ファイルパスの簡略化（絶対パスからプロジェクト相対パスっぽく見せる）
        $fileName = $class.filename
        if ($fileName -match "BmsPartTuner[\\/](.*)") {
            $fileName = $matches[1]
        }

        # 100%未満のクラスのみ抽出（完全なものはノイズになるため除外）
        if ($cLineRate -lt 100 -or $cBranchRate -lt 100) {
            $coverageDetails += [PSCustomObject]@{
                Class      = $class.name
                LineRate   = $cLineRate
                BranchRate = $cBranchRate
                File       = $fileName
            }
        }
    }

    # カバレッジが低い順（ワースト順）にソート
    $coverageDetails = $coverageDetails | Sort-Object LineRate

    Write-Host "Coverage Summary: Line=$($coverageSummary.LineRate), Branch=$($coverageSummary.BranchRate)" -ForegroundColor Green
} else {
    Write-Warning "Coverage file not found. Ensure 'coverlet.collector' package is installed."
}

# ---------------------------------------------------------
# JSON出力
# ---------------------------------------------------------

$aiContext = [PSCustomObject]@{
    Timestamp       = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    Summary         = @{
        TotalTests = $xml.TestRun.ResultSummary.Counters.total
        Failed     = $xml.TestRun.ResultSummary.Counters.failed
        Passed     = $xml.TestRun.ResultSummary.Counters.passed
    }
    CoverageSummary = $coverageSummary
    # ここが重要: 失敗テストと、カバレッジ不足ファイルを渡す
    FailedTests     = if ($failedTests) { $failedTests } else { @() }
    LowCoverageFiles = $coverageDetails
}

# ファイル書き出し
$aiContext | ConvertTo-Json -Depth 5 | Out-File $outputJson -Encoding utf8

if ($aiContext.Summary.Failed -gt 0) {
    Write-Host "Failure! Found $($aiContext.Summary.Failed) failed tests." -ForegroundColor Red
} else {
    Write-Host "All tests passed!" -ForegroundColor Green
}

Write-Host "AI Context generated at: $outputJson" -ForegroundColor Cyan
