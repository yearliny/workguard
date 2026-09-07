# GitHub 开发与构建

仓库：[yearliny/workguard](https://github.com/yearliny/workguard)，默认分支 `main`。

## 本地开发

```powershell
gh auth login
gh repo clone yearliny/workguard
cd workguard
dotnet run --project tests/WorkGuard.Tests -c Release
dotnet run --project src/WorkGuard.Windows
```

Windows 应用需要在 Windows 上运行；核心测试可在 Linux 或 Windows 上执行。提交前读取根目录 AGENTS.md。

## 自动验证与下载

每次推送 `main`、提交 PR 或手动启动工作流，GitHub Actions 会依次执行：

1. Linux 核心行为与持久化测试。
2. Windows Release 构建。
3. Windows 原生窗口、托盘及基础 API 启动检查。
4. 构建 Windows x64 自包含便携版和依赖运行时的精简版，生成校验文件。
5. 对两种打包后的程序分别进行启动检查。
6. 上传 `WorkGuard-版本-win-x64` 构建产物，保留 30 天。
7. main 或匹配版本的 v 标签构建通过后，自动发布尚未发布的项目版本到 Releases。

前往 [Actions](https://github.com/yearliny/workguard/actions)，打开成功运行，下载 Artifacts 中的便携包。需要登录具有仓库访问权限的 GitHub 账户。Actions 下载文件外层可能还有一层 ZIP，请解压至包含 WorkGuard.exe 及所有运行依赖的目录。

## Releases 策略

优先从 [Releases](https://github.com/yearliny/workguard/releases) 下载，无需查找具体 Actions 运行。发布标签为 `v` 加 `src/WorkGuard.Windows/WorkGuard.Windows.csproj` 中的 Version。

- 当前 MVP 自动发布为 prerelease，仓库仍保持私有。
- 测试、Windows 编译、两种发布包的启动检查全部成功后才执行发布。
- 发布 job 单独具有 `contents: write`，使用 GitHub 内置令牌，无需添加个人 token 或 secrets。
- 先创建草稿、上传文件、检查资产名称和大小，再公开为仓库内可见的预发布版。
- 同版本已发布时直接跳过，不覆盖其文件；下一次发布需要提升 Version。手动 v 标签必须与 Version 一致。
- 中断后只有相同源码提交可以续传自己的草稿，防止把同版本草稿换成另一份代码。
- Releases 文件不受 Actions 的 30 天保留策略限制。CI 仍保留 Actions 产物供排查。

窗口启动检查不替代多显示器、DPI、锁屏 / 睡眠和真实使用体验验收。
