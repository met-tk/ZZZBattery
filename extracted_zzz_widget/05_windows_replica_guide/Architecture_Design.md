# 绝区零 Windows 桌面小组件复刻架构设计与交付指南

本文档指导开发者基于已提取的逆向成果，在 Windows 平台上构建一个生产级、原汁原味的绝区零桌面电量悬浮小挂件。

---

## 一、 整体技术选型方案对比

| 技术方案 | 渲染引擎 | 打包体积 | 内存占用 | 适用场景 |
| :--- | :--- | :--- | :--- | :--- |
| **Python + Tkinter / CustomTkinter** | 原生系统绘图 | ~15MB (PyInstaller) | ~25MB | **极简轻量**，免外部运行时，开发测试最快 |
| **C# .NET 8 / WPF / WinUI 3** | DirectX / XAML | ~20MB (SingleFile) | ~35MB | **Windows 原生体验最佳**，完美适配高分屏缩放与亚克力磨砂磨砂玻璃效果 |
| **Flutter for Windows** | Skia / Impeller | ~25MB | ~40MB | **UI 视觉质感高**，易于跨平台移植至 macOS/Linux |
| **Electron / Tauri** | Webkit / WebView2 | 10MB(Tauri) ~ 70MB(Electron) | ~50MB | 熟悉前端 HTML/CSS 动效开发者的首选 |

---

## 二、 核心架构模块划分

```
+-------------------------------------------------------------+
|                     ZZZ Windows Widget                      |
+-------------------------------------------------------------+
   │
   ├── [1. 身份认证与凭据管理器 (Credential Manager)]
   │     ├─ 方案 A: 浏览器视窗登录 (WebView2 捕获 Cookie)
   │     ├─ 方案 B: 米游社 App 扫码登录 (Passport QR API)
   │     └─ 凭据存储: 本地加密持久化 (Windows DPAPI / CredentialVault)
   │
   ├── [2. 米游社网络通信层 (Net & API Layer)]
   │     ├─ DS 动态签名引擎 (DynamicSecret_DS)
   │     ├─ 设备头拦截器 (Device & Client Headers)
   │     └─ 接口请求适配 (Widget API & Daily Note API)
   │
   ├── [3. 业务与时钟推算引擎 (State & Interpolation Engine)]
   │     ├─ 周期同步器 (15~30分钟拉取一次服务端数据)
   │     ├─ 本地秒级插值时钟 (每秒更新倒计时，每360秒恢复1体力)
   │     └─ 离线缓存与风控熔断保护 (错误码 -100, 1034 自动降级)
   │
   └── [4. 桌面挂件渲染层 (Desktop UI & System Integration)]
         ├─ 潮酷深色/CRT 监控器风格无边框窗口 (可拖拽、鼠标穿透可选)
         ├─ 系统托盘集成 (托盘图标、右键菜单: 退出/置顶/开机自启)
         └─ 视觉切图与电量高亮配色
```

---

## 三、 关键 Windows 原生系统集成要点

### 1. 窗口置顶与鼠标穿透 (Click-Through)
- **置顶显示**：小组件通常悬浮在桌面一角，通过调用 Win32 API `SetWindowPos(HWND, HWND_TOPMOST, ...)` 实现。
- **锁定与穿透**：当用户在玩游戏或办公时，可开启“鼠标穿透”模式，窗口不可被点击，不遮挡下方操作：
  ```python
  # Windows 扩展样式设置 WS_EX_TRANSPARENT | WS_EX_LAYERED
  import win32gui, win32con
  hwnd = widget.winfo_id()
  win32gui.SetWindowLong(hwnd, win32con.GWL_EXSTYLE, 
                         win32gui.GetWindowLong(hwnd, win32con.GWL_EXSTYLE) | win32con.WS_EX_LAYERED | win32con.WS_EX_TRANSPARENT)
  ```

### 2. 开机自启 (Auto-Start)
在注册表 `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run` 添加应用可执行文件路径即可。

### 3. 系统托盘图标 (System Tray)
使用 Python 的 `pystray` 库或 C# 的 `NotifyIcon`，使小组件在最小化或后台运行时静默驻留在任务栏右下角通知区域。
