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

    $version = ([xml](Get-Content src/WorkGuard.Windows/WorkGuard.Windows.csproj -Raw)).Project.PropertyGroup.Version
    if ($version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw 'Invalid application version.' }

    $out = Join-Path $root "artifacts/publish/$Runtime/portable-lite"
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }

    & dotnet publish src/WorkGuard.Windows/WorkGuard.Windows.csproj -c Release -r $Runtime --self-contained false -p:PublishSingleFile=false -o $out
    if ($LASTEXITCODE -ne 0) { throw 'Portable framework-dependent publish failed.' }

    Get-ChildItem $out -Filter *.pdb -File -Recurse | Remove-Item
    Copy-Item README.md $out
    Copy-Item docs/PRIVACY.md $out
    Copy-Item docs/third-party $out -Recurse

    $instructions = @"
工作防沉迷 $version · 便携精简版

无需安装，完整解压后运行 WorkGuard.exe。
本包不包含 .NET 运行时，需要先安装 .NET 10 Desktop Runtime（与程序相同的 CPU 架构）。
Windows 自带的 .NET Framework 不能替代它。
官方下载：https://dotnet.microsoft.com/download/dotnet/10.0
"@
    Set-Content (Join-Path $out '开始使用.txt') -Value $instructions -Encoding utf8

    $name = "WorkGuard-$version-$Runtime-portable-lite.zip"
    $archive = Join-Path $root "artifacts/$name"
    Compress-Archive -Path "$out/*" -DestinationPath $archive -Force

    $hash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content (Join-Path $root "artifacts/SHA256SUMS-$Runtime.txt") -Value "$hash  $name" -Encoding ascii
    Write-Host "Created $name ($((Get-Item $archive).Length) bytes)"
}
finally {
    Pop-Location
}
