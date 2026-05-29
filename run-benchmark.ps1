$ErrorActionPreference = "Stop"

Write-Host "Running BenchmarkDotNet..."
dotnet run -c Release --project BmsAtelierKyokufu.BmsPartTuner.Benchmarks

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$destDir = "docs/benchmarks/history/$timestamp"
New-Item -ItemType Directory -Force -Path $destDir | Out-Null

$sourceDir = "BenchmarkDotNet.Artifacts/results"
if (Test-Path $sourceDir) {
    Copy-Item "$sourceDir/*" -Destination $destDir -Force
    Write-Host "`n[SUCCESS] Benchmark results saved to: $destDir"
} else {
    Write-Host "`n[WARNING] No benchmark results found in $sourceDir."
}
