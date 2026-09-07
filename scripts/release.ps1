param(
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][string]$Version,
    [string]$ArtifactsDirectory = 'artifacts'
)
$ErrorActionPreference = 'Stop'
if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Invalid repository.' }
if ($Commit -notmatch '^[a-f0-9]{40}$') { throw 'Expected a full commit SHA.' }
if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') { throw 'Invalid version.' }
$tag = "v$Version"
if ($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -ne $tag) {
    throw 'The pushed tag does not match the application Version.'
}
$expected = @("WorkGuard-$Version-win-x64-portable.zip", "WorkGuard-$Version-win-x64-lite.zip", 'SHA256SUMS-win-x64.txt')
$assets = @($expected | ForEach-Object {
    $path = Join-Path $ArtifactsDirectory $_
    if (-not (Test-Path $path -PathType Leaf)) { throw "Missing asset: $_" }
    (Resolve-Path $path).Path
})
# Check the files again after transfer from the Windows build job.
$hashLines = @(Get-Content (Join-Path $ArtifactsDirectory 'SHA256SUMS-win-x64.txt'))
if ($hashLines.Count -ne 2) { throw 'Expected two package checksums.' }
for ($i = 0; $i -lt 2; $i++) {
    $name = $expected[$i]
    $line = @($hashLines | Where-Object { $_ -match ('^[a-f0-9]{64}  ' + [regex]::Escape($name) + '$') })
    if ($line.Count -ne 1) { throw "Missing or duplicate checksum: $name" }
    if ((Get-FileHash $assets[$i] -Algorithm SHA256).Hash.ToLowerInvariant() -ne $line[0].Substring(0, 64)) {
        throw "Checksum mismatch: $name"
    }
}

# Versioned releases are immutable. Repeated builds of the same version never replace a published asset.
$json = & gh release list --repo $Repository --limit 100 --json tagName,isDraft
if ($LASTEXITCODE -ne 0) { throw 'Cannot query existing releases.' }
$existing = @($json | ConvertFrom-Json | Where-Object { $_.tagName -eq $tag })
if ($existing.Count -gt 0 -and -not $existing[0].isDraft) {
    Write-Host "$tag is already published; increment Version to publish another release."
    exit 0
}

$notes = @"
Windows MVP 试用版。源码：$Commit

## 选择下载包

- **portable.zip**：自带 .NET 桌面运行时，完整解压后运行 WorkGuard.exe，适合直接试用。
- **lite.zip**：精简包，需要预先安装 [.NET 10 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/10.0)。Windows 自带的 .NET Framework 不等于 .NET 10。
- **SHA256SUMS-win-x64.txt**：两个 ZIP 的校验值。

两种包功能相同。请完整解压，不要只复制 exe。

本版本已通过核心测试、Windows 编译，以及两种发布包的原生窗口启动检查。仍需真实使用中的混合 DPI、多显示器、锁屏与睡眠验收。当前未签名，不包含安装器或自动更新。
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
    Write-Host "Published https://github.com/$Repository/releases/tag/$tag"
} finally { if (Test-Path $notesFile) { Remove-Item $notesFile } }
