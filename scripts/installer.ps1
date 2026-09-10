param(
    [ValidateSet('win-x64')][string]$Runtime = 'win-x64',
    [ValidateSet('both', 'selfcontained', 'lite')][string]$Mode = 'both',
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

    $compiler = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    ) | Where-Object { $_ -and (Test-Path $_ -PathType Leaf) } | Select-Object -First 1
    if (-not $compiler) { throw 'Inno Setup 6 compiler (ISCC.exe) was not found.' }

    $artifactDir = Join-Path $root 'artifacts'
    New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
    $outputDir = (Resolve-Path $artifactDir).Path
    $flavors = if ($Mode -eq 'both') { @('selfcontained', 'lite') } else { @($Mode) }

    $checksumPath = Join-Path $artifactDir "SHA256SUMS-$Runtime.txt"
    $baseChecksumLines = if (Test-Path $checksumPath) {
        @(Get-Content $checksumPath | Where-Object {
            -not [string]::IsNullOrWhiteSpace($_) -and
            $_ -notmatch '  WorkGuard-.*-win-x64-setup-(selfcontained|lite)\.exe$'
        })
    } else { @() }
    $installerChecksumLines = @()

    foreach ($flavor in $flavors) {
        $selfContained = $flavor -eq 'selfcontained'
        $stage = Join-Path $root "artifacts/publish/$Runtime/installer-$flavor"
        if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }

        & dotnet publish src/WorkGuard.Windows/WorkGuard.Windows.csproj -c Release -r $Runtime --self-contained $selfContained.ToString().ToLowerInvariant() -p:PublishSingleFile=false -o $stage
        if ($LASTEXITCODE -ne 0) { throw "Installer payload publish failed: $flavor" }

        Get-ChildItem $stage -Filter *.pdb -File -Recurse | Remove-Item
        Copy-Item README.md $stage
        Copy-Item docs/PRIVACY.md $stage
        Copy-Item docs/third-party $stage -Recurse

        $sourceDir = (Resolve-Path $stage).Path
        $outputBase = "WorkGuard-$version-$Runtime-setup-$flavor"
        & $compiler "/DAppVersion=$version" "/DSourceDir=$sourceDir" "/DOutputDir=$outputDir" "/DOutputBase=$outputBase" installer/WorkGuard.iss
        if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed: $flavor" }

        $name = "$outputBase.exe"
        $installer = Join-Path $artifactDir $name
        if (-not (Test-Path $installer -PathType Leaf)) { throw "Installer was not created: $name" }

        $hash = (Get-FileHash $installer -Algorithm SHA256).Hash.ToLowerInvariant()
        $installerChecksumLines += "$hash  $name"

        Write-Host "Created $name ($((Get-Item $installer).Length) bytes)"
    }

    # Rebuild once, deterministically. This avoids scalar/array and blank-line
    # surprises from incrementally rewriting the checksum file per installer.
    Set-Content $checksumPath -Value @($baseChecksumLines + $installerChecksumLines) -Encoding ascii
}
finally {
    Pop-Location
}
