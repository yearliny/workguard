param(
    [ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64',
    [ValidateSet('both', 'portable', 'lite')][string]$Mode = 'both',
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
    $modes = if ($Mode -eq 'both') { @('portable', 'lite') } else { @($Mode) }
    $checksums = @()
    foreach ($flavor in $modes) {
        $out = Join-Path $root "artifacts/publish/$Runtime/$flavor"
        # This directory contains only this script's generated output.
        if (Test-Path $out) { Remove-Item $out -Recurse -Force }
        $selfContained = if ($flavor -eq 'portable') { 'true' } else { 'false' }
        & dotnet publish src/WorkGuard.Windows -c Release -r $Runtime --self-contained $selfContained -p:PublishSingleFile=false -o $out
        if ($LASTEXITCODE -ne 0) { throw "Publish failed: $flavor" }
        Get-ChildItem $out -Filter *.pdb -File -Recurse | Remove-Item
        Copy-Item README.md $out
        Copy-Item docs/PRIVACY.md $out
        if ($flavor -eq 'portable') { Copy-Item docs/third-party $out -Recurse }
        $instructions = if ($flavor -eq 'portable') {
            "工作防沉迷 $version · 便携版`n完整解压后运行 WorkGuard.exe。已包含 .NET 运行时，无需另行安装。"
        } else {
            "工作防沉迷 $version · 精简版`n需要先安装 .NET 10 Desktop Runtime（与程序相同的 CPU 架构）。`nWindows 自带的 .NET Framework 不能替代它。`n官方下载：https://dotnet.microsoft.com/download/dotnet/10.0`n安装后，完整解压本包并运行 WorkGuard.exe。"
        }
        Set-Content (Join-Path $out '开始使用.txt') -Value $instructions -Encoding utf8
        $name = "WorkGuard-$version-$Runtime-$flavor.zip"
        $archive = Join-Path $root "artifacts/$name"
        Compress-Archive -Path "$out/*" -DestinationPath $archive -Force
        $hash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
        $checksums += "$hash  $name"
        $size = (Get-Item $archive).Length
        Write-Host "Created $name ($size bytes)"
    }
    Set-Content (Join-Path $root "artifacts/SHA256SUMS-$Runtime.txt") -Value $checksums -Encoding ascii
} finally { Pop-Location }
