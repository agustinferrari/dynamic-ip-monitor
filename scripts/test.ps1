$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testDir = Join-Path $repoRoot 'test-data'
& (Join-Path $PSScriptRoot 'build.ps1') -Tests
$app = Join-Path $repoRoot 'bin\RegressionTests.exe'
$previousDataDir = $env:MONITORIP_DATA_DIR
try {
    $env:MONITORIP_DATA_DIR = $testDir
    New-Item -ItemType Directory -Path $testDir -Force | Out-Null
    $resultFile = Join-Path $testDir 'test-output.txt'
    $errorFile = Join-Path $testDir 'test-error.txt'
    $process = Start-Process -FilePath $app -WindowStyle Hidden -PassThru -RedirectStandardOutput $resultFile -RedirectStandardError $errorFile
    if (!$process.WaitForExit(30000)) { Stop-Process -Id $process.Id; throw 'Tests timed out.' }
    Get-Content -LiteralPath $resultFile
    if ($process.ExitCode -ne 0) { Get-Content -LiteralPath $errorFile; throw "Tests failed: $($process.ExitCode)" }
} finally {
    $env:MONITORIP_DATA_DIR = $previousDataDir
}
