$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path $repoRoot 'test-data'
$app = Join-Path $repoRoot 'bin\DynamicIPMonitor.exe'
if (!(Test-Path -LiteralPath $app)) { throw 'Run scripts/build.ps1 first.' }
$previousDataDir = $env:MONITORIP_DATA_DIR
try {
    $env:MONITORIP_DATA_DIR = $testDir
    New-Item -ItemType Directory -Path $testDir -Force | Out-Null
    $resultFile = Join-Path $testDir 'self-test-result.txt'
    if (Test-Path -LiteralPath $resultFile) { Remove-Item -LiteralPath $resultFile }
    $process = Start-Process -FilePath $app -ArgumentList '--self-test' -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(30000)) { Stop-Process -Id $process.Id; throw 'Tests timed out.' }
    if ($process.ExitCode -ne 0) { throw "Tests failed: $($process.ExitCode)" }
    if (!(Test-Path -LiteralPath $resultFile)) { throw 'Test result not produced.' }
    Get-Content -LiteralPath $resultFile
} finally {
    $env:MONITORIP_DATA_DIR = $previousDataDir
}
