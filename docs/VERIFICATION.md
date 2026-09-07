# 0.1.0 交付验证记录

## GitHub Actions：已通过

- 验证源码：`d6bda8b786f74d946c8abde5a0b5a6d0849afb7f`。
- [完整运行记录（首次运行）](https://github.com/yearliny/workguard/actions/runs/34078387890)。
- Linux Runner：28 项确定性行为测试全部通过。
- Windows Runner：完整 Release 构建成功，0 警告、0 错误。
- 原生启动检查：WPF 资源、四个窗口、托盘、输入空闲 API、前台窗口检测全部通过。
- Windows x64 自包含便携包发布成功，打包后的 WorkGuard.exe 再次通过相同启动检查。
- [下载已验证的便携包](https://github.com/yearliny/workguard/actions/runs/34078387890/artifacts/10002885588)，需要仓库访问权限，Actions 产物默认保留 30 天。

源码、构建配置和开发文档已提交到私有仓库 `yearliny/workguard`。后续仅更新验证记录和下载说明的文档提交，不改变以上已验证的应用源码。

## 本地验证

Linux x64、.NET SDK 10.0.400：Core / Data / Tests Release 编译通过，28 项行为测试全部通过，WPF 跨平台编译及 Windows x64 自包含发布成功。

此环境构建时使用 `BuildInParallel=false`、`UseSharedCompilation=false` 和 `-m:1`，以适应受限容器；行为测试直接由 `dotnet WorkGuard.Tests.dll` 执行。GitHub Runner 已验证 README 中的标准构建流程。

测试覆盖计时阈值、空闲与会议、睡眠间隙、推迟、活动完成与跳过、配置边界、持久化与损坏数据保护。

## 仍需人工验收

- Windows 日常使用中的混合 DPI、多显示器、真实锁屏 / 睡眠、会议软件兼容性及托盘行为。
- 界面视觉和可访问性验收，实际资源占用测量。
- 数字签名、安装器与专业健康内容审阅。

Windows Runner 的启动检查证明程序能够加载窗口和基础 Windows 集成，不等于完整实机体验验收。当前仍为 MVP 试用版。
