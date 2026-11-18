## SnugWin v{{VERSION}}

Windows 10/11 上为 网易 UU远程 提供手机原生级操作体验的伴侣程序。承接 [zephyr4123/snug](https://github.com/zephyr4123/snug)（MIT，v2.5.0）的产品体验与状态机，C# / .NET 10 LTS / WPF + virtual-display-rs。

### 下载

| 资产 | 说明 |
| --- | --- |
| `SnugWin-v{{VERSION}}-win-x64-setup.exe` | **一键安装包**（推荐新手）：内置 .NET 10 运行时、驱动校验安装脚本、桌面快捷方式与可选开机自启 |
| `SnugWin-v{{VERSION}}-win-x64-selfcontained.zip` | 绿色便携自包含版，内置 .NET 10 运行时，解压即用并支持应用内热更新 |
| `SnugWin-v{{VERSION}}-win-x64-frameworkdependent.zip` | 体积小，需先装 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0/runtime/desktop) |
| `virtual-display-driver-0.3.1-x64.zip` | 独立虚拟显示器驱动包（绿色版首次使用需要单独运行内部 `install-driver.cmd`） |

### 安装

- 安装版直接运行 `SnugWin-v{{VERSION}}-win-x64-setup.exe`，按向导完成安装。安装包内置运行时和虚拟显示驱动。
- 便携版解压后，首次使用时以管理员身份运行驱动包内的 `install-driver.cmd`，然后启动 `SnugWin.App.exe`。
- 手机通过 UU 远程连接电脑后，在屏幕列表中选择「Snug 手机屏」。

每个包内都附带 `快速开始.txt`。

### 本版变化

{{SUMMARY}}

{{COMMITS}}

{{COMPARE}}

### 前置条件

- Windows 10 2004+ / Windows 11，x64
- [virtual-display-rs](https://github.com/MolotovCherry/virtual-display-rs) 0.3.1（本 Release 已附带）
- 管理员权限（清单为 `requireAdministrator` + PerMonitorV2）

### 已知限制

- 仅在 Windows 11 x64 + UU远程 实测；Windows 10 2004+ 理论可用但未逐一验证
- 依赖 virtual-display-rs 的命名管道 `\\.\pipe\virtualdisplaydriver`
- 多屏场景（多台手机同时连入）尚未支持
- 换机型比例后，UU 侧需重选一次屏幕，点按位置才准确（设备与窗口不会重建）

### 致谢与许可

- 上游 macOS 原版：[zephyr4123/snug](https://github.com/zephyr4123/snug)（v2.5.0）
- 上游 Windows 版：[nicocart/snug-win](https://github.com/nicocart/snug-win)
- 虚拟显示器驱动：[MolotovCherry/virtual-display-rs](https://github.com/MolotovCherry/virtual-display-rs)（0.3.1）
- 本项目采用 MIT License。详情见 [第三方许可声明](https://github.com/Ruyi231/Snug/blob/main/THIRD_PARTY_NOTICES.md)。

