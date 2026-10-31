$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$distDir = Join-Path $repoRoot 'dist'
New-Item -ItemType Directory -Path $distDir -Force | Out-Null
$app = Join-Path $repoRoot 'bin\DynamicIPMonitor.exe'
if (!(Test-Path -LiteralPath $app)) { throw 'Run build.ps1 and test.ps1 first.' }
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($app).ProductVersion
if ($version -notmatch '^\d+\.\d+\.\d+(-[A-Za-z0-9.-]+)?$') { throw 'Invalid executable version.' }
if ($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -ne "v$version") { throw "Tag must match v$version" }
$zip = Join-Path $distDir "DynamicIPMonitor-$version-windows.zip"
# Explicit allowlist. Assets contains public README images, never runtime data.
Compress-Archive -LiteralPath $app,(Join-Path $repoRoot 'README.md'),(Join-Path $repoRoot 'README.en.md'),(Join-Path $repoRoot 'LICENSE'),(Join-Path $repoRoot 'CHANGELOG.md'),(Join-Path $repoRoot 'assets') -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($zip))" | Set-Content -LiteralPath "$zip.sha256" -Encoding ascii
Write-Output $zip
