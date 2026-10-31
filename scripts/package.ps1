$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$version = '0.3.0-beta'
if ($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -ne "v$version") { throw "Tag must match v$version" }
$distDir = Join-Path $repoRoot 'dist'
New-Item -ItemType Directory -Path $distDir -Force | Out-Null
$app = Join-Path $repoRoot 'bin\DynamicIPMonitor.exe'
if (!(Test-Path -LiteralPath $app)) { throw 'Run build.ps1 and test.ps1 first.' }
$zip = Join-Path $distDir "DynamicIPMonitor-$version-windows.zip"
# Explicit allowlist. Never package runtime state, settings, captures or histories.
Compress-Archive -LiteralPath $app,(Join-Path $repoRoot 'README.md'),(Join-Path $repoRoot 'README.en.md'),(Join-Path $repoRoot 'LICENSE'),(Join-Path $repoRoot 'CHANGELOG.md') -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($zip))" | Set-Content -LiteralPath "$zip.sha256" -Encoding ascii
Write-Output $zip
