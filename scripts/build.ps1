param([switch]$Tests)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework compiler not found.' }
$buildDir = Join-Path $repoRoot 'bin'
New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
$options = @('/nologo','/optimize+','/utf8output','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.Xml.dll','/reference:System.Security.dll')
$outputName = 'DynamicIPMonitor.exe'
if ($Tests) {
    $sourceFiles += Get-ChildItem -LiteralPath (Join-Path $repoRoot 'tests') -Filter '*.cs' | ForEach-Object { $_.FullName }
    $options += '/target:exe','/main:RegressionTests'
    $outputName = 'RegressionTests.exe'
} else { $options += '/target:winexe' }
& $compiler @options ("/out:" + (Join-Path $buildDir $outputName)) @sourceFiles
if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $LASTEXITCODE" }
Write-Output "Built bin/$outputName"
