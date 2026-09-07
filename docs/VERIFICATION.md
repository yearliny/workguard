# 0.1.0 交付验证记录

## 两种发布包与 Releases：已通过

- 验证源码与发布配置：`3c6254ef8f17f339ab171af9d61dc794b73a3a32`。
- [构建与发布运行记录](https://github.com/yearliny/workguard/actions/runs/34079812798)：核心测试、Windows 构建、两种包启动检查和 Releases 发布全部成功。
- [v0.1.0 预发布版](https://github.com/yearliny/workguard/releases/tag/v0.1.0)：已上传两种 ZIP 和 SHA256SUMS 校验文件。
- 便携版：76,017,994 字节（约 72.5 MiB），包含 .NET 桌面运行时。
- 精简版：119,068 字节（约 116.3 KiB），需要预装 .NET 10 Desktop Runtime x64。
- 发布作业已核对跨 job 传输后的 ZIP 校验值，以及 Releases 中资产的名称和大小，再将草稿发布为 prerelease。

两种包功能相同，差别仅为是否附带运行时。原包解压约 171 MiB，大部分为 .NET / WPF / Windows Forms 运行库，WorkGuard 自身文件约 335 KiB。新的压缩包移除了调试符号，并明确附带运行条件说明。

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
