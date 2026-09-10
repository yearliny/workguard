param(
    [Parameter(Mandatory)][string]$Version,
    [string]$ArtifactsDirectory = 'artifacts',
    [string]$OutputDirectory = 'artifacts/update-site'
)

$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw 'Invalid version.' }

# Automatic updates deliberately use the self-contained installer so update
# success never depends on whether the target PC already has the .NET runtime.
$installerName = "WorkGuard-$Version-win-x64-setup-selfcontained.exe"
$installer = Join-Path $ArtifactsDirectory $installerName
$checksums = Join-Path $ArtifactsDirectory 'SHA256SUMS-win-x64.txt'
if (-not (Test-Path $installer -PathType Leaf)) { throw "Missing installer: $installerName" }
if (-not (Test-Path $checksums -PathType Leaf)) { throw 'Missing checksum file.' }

$line = @(Get-Content $checksums | Where-Object { $_ -match ('^[a-f0-9]{64}  ' + [regex]::Escape($installerName) + '$') })
if ($line.Count -ne 1) { throw 'Installer checksum not found.' }
$sha = $line[0].Substring(0, 64)
if ((Get-FileHash $installer -Algorithm SHA256).Hash.ToLowerInvariant() -ne $sha) { throw 'Installer checksum mismatch.' }

if (Test-Path $OutputDirectory) { Remove-Item $OutputDirectory -Recurse -Force }
$downloads = Join-Path $OutputDirectory 'downloads'
$update = Join-Path $OutputDirectory 'update'
New-Item -ItemType Directory -Force -Path $downloads, $update | Out-Null
Copy-Item $installer (Join-Path $downloads $installerName)

$manifest = [ordered]@{
    version = $Version
    url = "https://yearliny.github.io/workguard/downloads/$installerName"
    sha256 = $sha
    size = (Get-Item $installer).Length
}
$manifest | ConvertTo-Json | Set-Content (Join-Path $update 'win-x64.json') -Encoding utf8
@"
<!doctype html>
<html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>WorkGuard Updates</title><body><p>WorkGuard update distribution endpoint.</p></body></html>
"@ | Set-Content (Join-Path $OutputDirectory 'index.html') -Encoding utf8
Set-Content (Join-Path $OutputDirectory '.nojekyll') -Value '' -Encoding ascii

Write-Host "Prepared public self-contained update feed for $Version"
