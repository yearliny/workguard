# 0.4.0 强制休息验证

- 本地完整 Release 编译通过，0 警告、0 错误；42 项核心行为测试通过。
- 核心新增验证：强制范围、设置持久化、拒绝跳过、替代活动完成语义、暂停 / 长间隙、静默与自然离席、所有场景时长。
- Windows CI 增加：显示即开始、隐藏及拒绝普通跳过 / 暂停、关闭确认、完成自动释放、紧急退出不计数、系统暂停后继续、替代休息统计集成、辅助屏窗口释放。
- Windows 截图新增强制设置、离座、肩颈、退出确认、安静休息和远眺状态。CI 结果与截图见本次 PR 的 Actions；合并后的构建通过才自动发布 v0.4.0。
- 多屏 API 已实现，CI 会按 Runner 实际显示器数量运行；混合 DPI、真实多屏热插拔、系统锁屏 / 睡眠、Win10 / Win11 长时间使用仍需实机验收。保持预发布标记。

---

# 0.3.0 视觉更新验证

- [改进 PR #2](https://github.com/yearliny/workguard/pull/2)：离线插画、原生图标、设置控件与活动布局。
- Windows 原生截图已检查，见 [今日概览](images/dashboard-v0.3.png)；图片使用 CI 演示数据。
- 原生检查验证内嵌插画可加载、解码宽度受限、当前图标字体包含所有使用的字形，以及设置校验、暂停、完成和跳过交互。
- 35 项核心行为测试通过；Windows 编译通过。打包已改为显式指定项目，忽略 WPF 临时项目文件。
- 合并后 CI 重新检查并自动发布精简包，结果见 [v0.3.0 Releases](https://github.com/yearliny/workguard/releases/tag/v0.3.0)。
- 沿用下列实机验收边界：没有代码签名，尚需 Win10 / Win11、多屏、混合 DPI 与辅助技术实测；保持预发布标记。

---

# 0.2.0 验证记录

- 改进记录：[PR #1](https://github.com/yearliny/workguard/pull/1)。
- [Windows 检查与精简包构建](https://github.com/yearliny/workguard/actions/runs/34082247704)：35 项确定性测试、Release 编译、原生界面与交互检查、精简包启动检查通过。
- Windows UI 截图保存在对应 Actions 的 `windows-ui` 产物中（30 天），覆盖今日、历史、三组设置、轻提醒、眼睛准备、身体准备 / 进行 / 暂停 / 完成共 11 个状态。
- 已人工查看这些真实 WPF 渲染截图，并修正首次布局列宽、窗口样式、活动过早全屏、常用入口位置和按钮文字对比度。
- 本地 Linux 使用 .NET SDK 10.0.400 编译完整解决方案，0 警告、0 错误；35 项行为测试通过。原生窗口运行验证在 Windows Runner 执行。
- 新增测试覆盖安静时段端点 / 跨午夜、跨日统计、区域设置无关的 CSV、有效 / 损坏备份恢复、旧偏好兼容。
- Windows 交互检查覆盖设置非法输入、提醒焦点策略、暂停冻结、完整流程只记一次、跳过不记完成。

版本 0.2.0 由合并后的主分支 CI 再次构建与检查，通过后自动创建精简 ZIP 和 SHA-256 校验文件的 Releases 预发布版。具体发布结果请查看 [v0.2.0](https://github.com/yearliny/workguard/releases/tag/v0.2.0) 和对应运行记录。

## 尚未完成的生产验收

Windows Runner 是自动检查环境，不等于 Win10 / Win11 实机日常使用验收。混合 DPI、多屏、真实锁屏 / 睡眠、会议兼容、辅助技术、长时间资源占用及数字签名仍待完成，详见 [人工验收清单](WINDOWS-ACCEPTANCE.md)。本版本保留预发布标记，不宣称已完成全部生产认证。

---

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
