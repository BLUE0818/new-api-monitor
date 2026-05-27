# CCTQ Monitor

轻量级 Windows 悬浮窗，用于通过 CCTQ 的 System Access Token 监控余额、今日消耗和连接状态。

## 功能

- 每 30 秒刷新一次总余额和今日消耗。
- 余额与今日消耗保留两位小数。
- 延迟不显示具体数字，只用状态灯表达：
  - 绿色：0-3 秒
  - 黄色：3-10 秒
  - 红色：10 秒以上
  - 灰色：请求失败、认证失败或超时
- 首次启动填写站点 URL、System Access Token、New-Api-User 用户 ID。
- Token 使用 Windows DPAPI 加密保存在当前 Windows 用户下。
- 支持悬浮窗拖动、锁定位置、置顶、系统托盘菜单。

## 分发

Release 构建产物位于：

```text
CctqMonitor/bin/Release/CCTQ Monitor.exe
```

最终用户直接运行这个 exe 即可。当前实现基于 .NET Framework 4.7.1，Windows 10/11 通常已有兼容运行时；不需要安装 Node、.NET SDK 或其他第三方依赖。

## 构建

本机没有 .NET SDK，因此项目使用 Visual Studio/MSBuild 的经典 .NET Framework WPF 项目格式。

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" CctqMonitor\CctqMonitor.csproj /p:Configuration=Release
```

`new-api/` 只是参考仓库，已被 `.gitignore` 排除，不属于本项目源码。
