param(
    [ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64',
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if (-not $SkipTests) {
        & dotnet run --project tests/WorkGuard.Tests -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Behavior tests failed.' }
    }
    $out = Join-Path $root "artifacts/publish/$Runtime"
    # This directory contains only this script's generated output.
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    & dotnet publish src/WorkGuard.Windows -c Release -r $Runtime --self-contained true -p:PublishSingleFile=false -o $out
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item README.md $out
    Copy-Item docs/PRIVACY.md $out
    Copy-Item docs/VERIFICATION.md $out
    Copy-Item docs/third-party $out -Recurse
    $archive = Join-Path $root "artifacts/WorkGuard-0.1.0-$Runtime.zip"
    Compress-Archive -Path "$out/*" -DestinationPath $archive -Force
    Get-FileHash $archive -Algorithm SHA256 | Format-List
    Write-Host "Created $archive"
} finally { Pop-Location }
