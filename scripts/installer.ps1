param(
    [ValidateSet('win-x64')][string]$Runtime = 'win-x64',
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

    $version = ([xml](Get-Content src/WorkGuard.Windows/WorkGuard.Windows.csproj -Raw)).Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw 'Invalid application version.' }

    $stage = Join-Path $root "artifacts/publish/$Runtime/installer-app"
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }

    & dotnet publish src/WorkGuard.Windows/WorkGuard.Windows.csproj -c Release -r $Runtime --self-contained true -p:PublishSingleFile=false -o $stage
    if ($LASTEXITCODE -ne 0) { throw 'Self-contained installer payload publish failed.' }

    Get-ChildItem $stage -Filter *.pdb -File -Recurse | Remove-Item
    Copy-Item README.md $stage
    Copy-Item docs/PRIVACY.md $stage
    Copy-Item docs/third-party $stage -Recurse

    $compiler = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    ) | Where-Object { $_ -and (Test-Path $_ -PathType Leaf) } | Select-Object -First 1
    if (-not $compiler) { throw 'Inno Setup 6 compiler (ISCC.exe) was not found.' }

    $artifactDir = Join-Path $root 'artifacts'
    New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
    $sourceDir = (Resolve-Path $stage).Path
    $outputDir = (Resolve-Path $artifactDir).Path

    & $compiler "/DAppVersion=$version" "/DSourceDir=$sourceDir" "/DOutputDir=$outputDir" installer/WorkGuard.iss
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }

    $name = "WorkGuard-$version-$Runtime-setup.exe"
    $installer = Join-Path $artifactDir $name
    if (-not (Test-Path $installer -PathType Leaf)) { throw "Installer was not created: $name" }

    $hash = (Get-FileHash $installer -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksumPath = Join-Path $artifactDir "SHA256SUMS-$Runtime.txt"
    $existing = if (Test-Path $checksumPath) { @(Get-Content $checksumPath | Where-Object { $_ -notmatch ('  ' + [regex]::Escape($name) + '$') }) } else { @() }
    Set-Content $checksumPath -Value @($existing + "$hash  $name") -Encoding ascii

    Write-Host "Created $name ($((Get-Item $installer).Length) bytes)"
}
finally {
    Pop-Location
}
