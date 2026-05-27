# new-api-monitor

一个轻量级 Windows 悬浮窗，用于通过 new-api / CCTQ 的系统访问令牌监控账户余额、今日消耗和站点延迟。

![预览](assets/preview.png)

## 功能

- 每 30 秒自动刷新余额和今日消耗。
- 余额与今日消耗按站点配置换算后显示，保留两位小数。
- 延迟显示在状态灯右侧，保留两位小数，单位统一为 `s`。
- 状态灯颜色：
  - 绿色：0-3 秒
  - 黄色：3-10 秒
  - 红色：10 秒以上
  - 灰色：请求失败、认证失败或超时
- 首次启动只需填写：
  - 系统访问令牌：个人设置-安全设置-系统访问令牌
  - 用户 ID：个人设置-ID
- 站点地址内置为 `https://www.cctq.ai`。
- 令牌使用 Windows DPAPI 加密保存在当前 Windows 用户下。
- 支持悬浮窗拖动、锁定位置、置顶、系统托盘菜单。

## 分发

直接分发 Release 构建生成的单个文件即可：

```text
CctqMonitor/bin/Release/CCTQ Monitor.exe
```

用户运行时不会在 exe 同级目录生成配置文件。配置保存到：

```text
%APPDATA%\CCTQ Monitor\config.json
```

当前实现基于 .NET Framework 4.7.1。Windows 10/11 通常已有兼容运行时，不需要安装 Node、.NET SDK 或其他第三方依赖。

## 构建

项目使用经典 .NET Framework WPF 项目格式，可通过 Visual Studio/MSBuild 构建：

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" CctqMonitor\CctqMonitor.csproj /p:Configuration=Release
```

## 技术栈

- C#
- WPF
- .NET Framework 4.7.1
- Windows Forms `NotifyIcon`
- `System.Net.Http.HttpClient`
- Windows DPAPI

## 说明

`new-api/` 是开发时参考的上游项目仓库，已被 `.gitignore` 排除，不属于本项目源码。

## License

MIT
