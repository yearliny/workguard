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
工作防沉迷 $Version · 每天一点，活动自如。源码：$Commit

- 每日 5–10 分钟身体维护目标，默认 8 分钟；支持约两分钟短维护和集中完成今日剩余，准备时间单独显示。
- 13 个基础动作，按当天已确认的部位覆盖量安排；可排除部位、站姿和指定动作，颈部动作默认关闭。
- 跟练提供原生矢量示意、准备与左右侧提示、清楚的当前及整场倒计时，以及可选 Windows 中文语音。
- 不适可立即停止并暂停对应动作。结束后由用户确认实际跟练，暂停、睡眠、跳过不会自动记为完整跟练。
- 普通身体休息与 7 分钟活动同样尊重动作限制；每日记录保存在本机，支持七日历史和 CSV 导出。
- 78 项核心行为测试，以及 Windows 原生交互和精简包检查。

入口：首页 → 身体维护。自动安排需在“维护偏好”中开启，升级后默认关闭。

数据格式升级至 schema 2，兼容读取旧记录；旧版程序会以只读方式保护新格式。动作示意是一般活动提示，不代表已经过临床或康复专业审核；中文语音听感和动作内容仍需实机体验及专业复核。

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
