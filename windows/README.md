# Live Dashboard — Windows Agent

监控前台窗口并向 Live Dashboard 后端上报应用使用状态，支持系统托盘常驻运行。

## 快速开始

从 [GitHub Releases](https://github.com/Monika-Dream/live-dashboard/releases) 下载 `LiveDashboardAgent.exe`，将 `config.json` 放在同目录下，双击运行即可。

## 从源码运行

**需要**: .NET 9 SDK

1. 还原依赖并构建：
   ```bash
   cd LiveAgent
   dotnet build
   ```
2. 复制 `config.example.json` 为 `config.json`，填入你的信息：
   ```json
   {
     "server_url": "https://your-domain.com",
     "token": "你的设备密钥",
     "interval_seconds": 5,
     "heartbeat_seconds": 60,
     "idle_threshold_seconds": 300
   }
   ```
3. 运行：
   ```bash
   dotnet run --project LiveAgent
   ```

## 打包为单文件 .exe

在 `LiveAgent/` 目录下运行 `build.bat`，会清理旧产物并发布为自包含单文件 `publish/LiveDashboardAgent.exe`（约 50MB，内含 .NET 运行时和全部依赖）。

也可手动执行：
```bash
dotnet publish LiveAgent/LiveAgent.csproj -c Release -o publish
```

将 `config.json` 放在 `.exe` 同目录下即可运行。

## 开机自启

在托盘右键菜单中点击"开机自启"即可切换。程序通过写入当前用户的注册表 Run 键 (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`) 实现登录自启动，无需管理员权限。关闭时会同时清理旧版任务计划条目（如有）。

## 配置说明

| 字段 | 说明 | 默认值 |
|------|------|--------|
| `server_url` | 后端地址 | 必填 |
| `token` | 设备密钥（部署服务端时生成的） | 必填 |
| `interval_seconds` | 上报间隔（秒），范围 1–300 | `5` |
| `heartbeat_seconds` | AFK 时心跳间隔（秒），范围 10–600 | `60` |
| `idle_threshold_seconds` | 无操作多久后进入 AFK 模式（秒），范围 30–3600 | `300` |
| `enable_log` | 是否写入日志文件 | `false` |

## 功能

### 系统托盘

启动后常驻系统托盘，图标颜色反映当前状态（绿色=在线，橙色=AFK，灰色=离线/初始化）。

右键菜单：
- 查看当前状态和正在使用的应用
- 开关日志文件
- 开关开机自启
- 设置对话框（编辑服务器地址、Token、上报间隔等）
- 安全退出

### 前台应用检测

实时检测当前前台窗口的应用和标题，通过 Win32 API (`GetForegroundWindow` / `GetWindowTextW` / `GetWindowThreadProcessId`) 获取进程信息。

### 音乐检测

自动识别 Spotify、QQ音乐、网易云、foobar2000、酷狗、酷我、AIMP、VLC、PotPlayer、Windows Media Player 等播放器，通过 `EnumWindows` 扫描所有可见窗口，从窗口标题解析正在播放的歌曲信息。

### 电量上报

笔记本自动上报电池电量和充电状态，台式机不显示（正常行为）。通过 `System.Windows.Forms.SystemInformation.PowerStatus` 获取。

### AFK 检测

无键鼠输入超过阈值（默认 300 秒）后进入 AFK 模式，切换为低频心跳上报。

**视频/音频免 AFK**：当检测到以下情况时，即使键鼠空闲也不会进入 AFK：
- 系统有活跃音频流（通过 NAudio 的 Windows Core Audio API 检测所有活跃音频会话）
- 前台窗口处于全屏状态（通过 Win32 API 比对窗口尺寸与屏幕分辨率）

典型场景：看视频、听音乐、全屏演示时不会被标记为 AFK。

### 日志

运行日志写入 `agent.log`，按天轮转保留 2 天。默认关闭，可通过托盘菜单或配置文件开启。

## 技术栈

- **.NET 9 WinForms**: 应用框架、系统托盘 (`NotifyIcon`)、设置对话框
- **Win32 P/Invoke**: 前台窗口检测、空闲时间、全屏检测、`EnumWindows`
- **NAudio**: Windows Core Audio API，检测活跃音频会话
- **System.Text.Json**: 配置文件读写
- **Microsoft.Win32**: 注册表自启动管理
