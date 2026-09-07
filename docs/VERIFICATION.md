# 0.1.0 交付验证记录

验证环境：Linux x64，.NET SDK 10.0.400；目标发布为 Windows x64、包含运行时。

## 已执行

- Core / Data / Tests Release 编译成功，0 警告、0 错误。
- 28 项确定性行为测试全部通过，覆盖计时阈值、空闲与会议、睡眠间隙、推迟、活动完成与跳过、配置边界、持久化与损坏数据保护。
- WPF Windows 项目跨平台 Release 编译成功，0 警告、0 错误，包含 XAML 编译。
- 最新源码 `dotnet publish -r win-x64 --self-contained true` 成功，生成 `WorkGuard.exe` 和运行依赖。

此环境中构建时使用 `BuildInParallel=false`、`UseSharedCompilation=false` 和 `-m:1`，避免受限容器中的 MSBuild 工作进程问题。行为测试直接由 `dotnet WorkGuard.Tests.dll` 执行。项目在正常 Windows / GitHub Runner 上使用 README 中的标准命令。

## 尚未执行

- Windows 实机运行、界面截图检查、混合 DPI、多显示器、实际锁屏 / 睡眠 / 托盘验证。
- `scripts/smoke-windows.ps1` 已编写并接入 CI，但当前 Linux 环境不能执行 WPF 程序。
- GitHub 仓库已由用户创建；首次源码提交与 Windows Actions 验证正在进行，结果以对应运行记录为准。
- 数字签名、安装器、真实资源占用测量及专业健康内容审阅。

Windows 包是已编译的 MVP 试用包，不应把跨平台编译通过表述为 Windows 实机验收或正式发布完成。
