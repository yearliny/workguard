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
$expected = @("WorkGuard-$Version-win-x64-lite.zip", 'SHA256SUMS-win-x64.txt')
$assets = @($expected | ForEach-Object {
    $path = Join-Path $ArtifactsDirectory $_
    if (-not (Test-Path $path -PathType Leaf)) { throw "Missing asset: $_" }
    (Resolve-Path $path).Path
})
# Check the files again after transfer from the Windows build job.
$hashLines = @(Get-Content (Join-Path $ArtifactsDirectory 'SHA256SUMS-win-x64.txt'))
if ($hashLines.Count -ne 1) { throw 'Expected one package checksum.' }
for ($i = 0; $i -lt 1; $i++) {
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
工作防沉迷 $Version · 到点就休息。源码：$Commit

- 设置 → 强制休息：开启后到点直接全屏，眼睛 20 秒 / 身体 3 分钟，可先体验再保存。
- 多屏同步；不能普通跳过或推迟，结束自动返回。Esc 打开紧急退出确认，保留系统快捷键。
- 远眺、离座、肩颈、手腕场景轮换；温和转头默认关闭，可单独开启。
- 动作不适合可改为安静休息，时长不缩短，不记身体活动。会议、安静时段和全屏静默仍有效。
- 升级保留原来的温和模式；42 项核心行为测试，新增 Windows 强制交互及统计验证。


## 下载

仅提供 **lite.zip** 精简版，含本地插画。需要 [.NET 10 Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/10.0)。完整解压后运行 WorkGuard.exe；Windows 内置的 .NET Framework 不能替代此运行时。

目标为 Windows 10 22H2 / Windows 11 x64。SHA256SUMS-win-x64.txt 提供 ZIP 校验值。

经过核心行为测试、Windows 编译、真实 WPF 窗口与交互检查、发布包启动检查。Windows CI 不等于 Win10/Win11 实机长时间验收；混合 DPI、多屏、锁屏与睡眠仍需人工验收。当前未签名，没有安装器或自动更新，因此保留预发布标记。

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
