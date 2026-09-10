param(
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][string]$Version,
    [string]$ArtifactsDirectory = 'artifacts'
)
$ErrorActionPreference = 'Stop'

function Set-PublishedOutput([bool]$value) {
    if ($env:GITHUB_OUTPUT) { Add-Content $env:GITHUB_OUTPUT ("published=" + $value.ToString().ToLowerInvariant()) }
}
Set-PublishedOutput $false

if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Invalid repository.' }
if ($Commit -notmatch '^[a-f0-9]{40}$') { throw 'Expected a full commit SHA.' }
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw 'Invalid version.' }
$tag = "v$Version"
if ($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -ne $tag) {
    throw 'The pushed tag does not match the application Version.'
}

$packages = @(
    "WorkGuard-$Version-win-x64-setup-selfcontained.exe",
    "WorkGuard-$Version-win-x64-setup-lite.exe",
    "WorkGuard-$Version-win-x64-portable-lite.zip"
)
$expected = @($packages + 'SHA256SUMS-win-x64.txt')
$assets = @($expected | ForEach-Object {
    $path = Join-Path $ArtifactsDirectory $_
    if (-not (Test-Path $path -PathType Leaf)) { throw "Missing asset: $_" }
    (Resolve-Path $path).Path
})

$hashLines = @(Get-Content (Join-Path $ArtifactsDirectory 'SHA256SUMS-win-x64.txt') |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($hashLines.Count -ne $packages.Count) { throw "Expected $($packages.Count) package checksums." }
foreach ($name in $packages) {
    $line = @($hashLines | Where-Object { $_ -match ('^[a-f0-9]{64}  ' + [regex]::Escape($name) + '$') })
    if ($line.Count -ne 1) { throw "Missing or duplicate checksum: $name" }
    if ((Get-FileHash (Join-Path $ArtifactsDirectory $name) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $line[0].Substring(0, 64)) {
        throw "Checksum mismatch: $name"
    }
}

$json = & gh release list --repo $Repository --limit 100 --json tagName,isDraft
if ($LASTEXITCODE -ne 0) { throw 'Cannot query existing releases.' }
$existing = @($json | ConvertFrom-Json | Where-Object { $_.tagName -eq $tag })
if ($existing.Count -gt 0 -and -not $existing[0].isDraft) {
    Write-Host "$tag is already published; increment Version to publish another release."
    exit 0
}

$notes = @"
工作防沉迷 $Version · 每天一点，活动自如。源码：$Commit

本版本将 Windows 分发明确拆成三种形态：

1. **Installer + self-contained .NET**：普通用户推荐，无需预装 .NET，安装后即可使用；自动更新也使用这个版本。
2. **Installer + 不带 .NET**：安装体验完整，但需要预先安装 .NET 10 Desktop Runtime x64，安装包更小。
3. **便携版 + 不带 .NET**：无需安装，解压即用，同样需要 .NET 10 Desktop Runtime x64。

## 下载建议

- 首选 **WorkGuard-$Version-win-x64-setup-selfcontained.exe**：最省心，无需单独安装 .NET。
- 已安装 .NET 10 Desktop Runtime 的用户可选 **WorkGuard-$Version-win-x64-setup-lite.exe**，获得更小的安装包。
- 不希望安装程序的用户可选 **WorkGuard-$Version-win-x64-portable-lite.zip**，完整解压后直接运行 `WorkGuard.exe`。

自动更新始终下载 self-contained Installer，从而不依赖目标电脑当前是否安装 .NET。

目标为 Windows 10 22H2 / Windows 11 x64。`SHA256SUMS-win-x64.txt` 提供全部三个发布包的 SHA-256 校验值。

当前构建尚未进行商业代码签名，因此 Windows SmartScreen 仍可能提示未知发布者。
"@

$notesFile = Join-Path ([IO.Path]::GetTempPath()) ("workguard-release-" + [guid]::NewGuid() + '.md')
try {
    Set-Content $notesFile -Value $notes -Encoding utf8
    if ($existing.Count -eq 0) {
        & gh release create $tag --repo $Repository --target $Commit --title "工作防沉迷 $tag" --notes-file $notesFile --draft --prerelease
        if ($LASTEXITCODE -ne 0) { throw 'Draft release creation failed.' }
    } else {
        $draftJson = & gh release view $tag --repo $Repository --json targetCommitish
        if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect draft release.' }
        if (($draftJson | ConvertFrom-Json).targetCommitish -ne $Commit) {
            throw 'Existing draft belongs to a different source commit; refusing to replace its assets.'
        }
    }

    & gh release upload $tag @assets --repo $Repository --clobber
    if ($LASTEXITCODE -ne 0) { throw 'Asset upload failed; release remains a draft.' }

    $uploadedJson = & gh release view $tag --repo $Repository --json assets
    if ($LASTEXITCODE -ne 0) { throw 'Cannot verify uploaded assets.' }
    $uploaded = ($uploadedJson | ConvertFrom-Json).assets
    foreach ($name in $expected) {
        $match = @($uploaded | Where-Object { $_.name -eq $name })
        if ($match.Count -ne 1 -or $match[0].size -ne (Get-Item (Join-Path $ArtifactsDirectory $name)).Length) {
            throw "Uploaded asset is incomplete: $name"
        }
    }

    & gh release edit $tag --repo $Repository --draft=false --prerelease --latest=false
    if ($LASTEXITCODE -ne 0) { throw 'Publishing the verified draft failed.' }
    Set-PublishedOutput $true
    Write-Host "Published https://github.com/$Repository/releases/tag/$tag"
}
finally {
    if (Test-Path $notesFile) { Remove-Item $notesFile }
}
