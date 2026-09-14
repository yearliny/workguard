# 自动更新下载代理

新版读取 `https://yearliny.github.io/workguard/update/win-x64-proxy.json`，清单中的安装包为本仓库 Releases 原始地址。客户端只接受与清单版本完全匹配的 self-contained x64 安装器，下载时添加 `https://ghfast.top/` 前缀。

清单与 SHA-256 保持来自 GitHub Pages，不经过第三方代理。代理只负责公开安装包，不发送 Token 或本地活动记录。大小与 SHA-256 不匹配时拒绝安装并删除临时文件；不自动回退直连。

保留旧 `win-x64.json` 和 Pages 安装包副本，使只信任 Pages 的旧客户端仍可升级。新客户端也能识别同版本的旧 Pages 安装包地址，并映射到 Releases 代理下载；不允许清单任意指定其他仓库、其他文件或嵌套代理。

Windows 冒烟检查使用内存 HTTP 响应验证实际请求地址、正常下载、同大小文件哈希不符及临时文件清理，无需访问第三方服务或执行安装器。外部代理可用性与国内下载速度不属于该测试保证。
