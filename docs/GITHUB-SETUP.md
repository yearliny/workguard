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
4. Windows x64 自包含便携版打包。
5. 对打包后的程序再次进行启动检查。
6. 上传 `WorkGuard-0.1.0-win-x64` 构建产物，保留 30 天。

前往 [Actions](https://github.com/yearliny/workguard/actions)，打开成功运行，下载 Artifacts 中的便携包。需要登录具有仓库访问权限的 GitHub 账户。Actions 下载文件外层可能还有一层 ZIP，请解压至包含 WorkGuard.exe 及所有运行依赖的目录。

工作流不会自动发布公开 Release。窗口启动检查不替代多显示器、DPI、锁屏 / 睡眠和真实使用体验验收。
