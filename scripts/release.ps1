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
    "WorkGuard-$Version-win-x64-lite.zip",
    "WorkGuard-$Version-win-x64-setup.exe"
)
$expected = @($packages + 'SHA256SUMS-win-x64.txt')
$assets = @($expected | ForEach-Object {
    $path = Join-Path $ArtifactsDirectory $_
    if (-not (Test-Path $path -PathType Leaf)) { throw "Missing asset: $_" }
    (Resolve-Path $path).Path
})

$hashLines = @(Get-Content (Join-Path $ArtifactsDirectory 'SHA256SUMS-win-x64.txt'))
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

- 正式提供 Windows 安装器：单用户安装，无需管理员权限，内置 .NET 运行时，安装后可直接使用。
- 自动更新：后台低频检查公开更新清单，发现新版本后自动下载并校验 SHA-256；用户可从系统托盘一键安装。
- 源码仓库保持私有；公开分发端只包含安装包与最小更新清单，不暴露源码或访问令牌。
- 同时保留 lite.zip 精简版，适合已经安装 .NET 10 Desktop Runtime 的用户。
- 应用图标、任务栏和托盘统一为轻量品牌图标。
- 登录 Windows 时启动支持在设置中关闭；安装目录变化后会自动修复启动项路径。

## 下载建议

普通用户请选择 **WorkGuard-$Version-win-x64-setup.exe**。安装到当前用户目录，不需要管理员权限，也不需要单独安装 .NET。

高级用户可选择 **WorkGuard-$Version-win-x64-lite.zip**；该版本需要 .NET 10 Desktop Runtime x64。

目标为 Windows 10 22H2 / Windows 11 x64。`SHA256SUMS-win-x64.txt` 提供所有发布包的 SHA-256 校验值。

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
