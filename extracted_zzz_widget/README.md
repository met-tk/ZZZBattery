# 米游社 APK 绝区零 (ZZZ) 桌面电量小组件逆向与 Windows 端复刻成果

本项目基于工作区中的【米游社.apk】（版本：`2.114.0`）进行深度逆向分析，成功定位并提取出其自带的**绝区零桌面电量监测与战绩便笺小组件**的完整代码逻辑、接口契约、动态签名算法及视觉切图资源，并系统化分类归档。

---

## 目录结构索引

```
extracted_zzz_widget/
├── README.md                                 # 本项目总览与使用指南
│
├── 01_api_and_network/                       # 【第一分类：网络通信与接口协议】
│   ├── ApiEndpoints.md                       # 逆向提取出的所有官方后端 API 接口规范与参数
│   ├── Headers_Specification.md              # 客户端请求头 (x-rpc-*) 规范与错误码映射说明
│   ├── DynamicSecret_DS.py                   # 纯 Python 实现的米游社动态密钥 (DS v1/v2) 签名算法
│   ├── ZzzWidgetRetrofitApi.java             # 提取自 classes12.dex 的原版 Retrofit 接口源码 (a.java)
│   └── ZzzAvatarWidgetRetrofitApi.java       # 提取自 classes12.dex 的原版活动小组件 Retrofit 接口
│
├── 02_data_models/                           # 【第二分类：数据实体模型】
│   ├── NapWidgetData.java                    # 提取自原版 APK 的 NapWidgetData 完整 Java 实体类
│   ├── AvatarBackground.java                 # 提取自原版 APK 的角色背景数据实体类
│   └── NapWidgetDataModel.py                 # 跨平台/Python 版绝区零电量与便笺数据模型类
│
├── 03_business_logic/                        # 【第三分类：业务与时钟推算逻辑】
│   ├── NapWidgetWorker.java                  # 原版电量小组件后台 Worker 刷新调度源码
│   ├── NapWidgetProvider.java                # 原版 AppWidgetProvider 广播接收器源码
│   ├── NapAvatarWidgetWorker.java            # 原版活动角色小组件 Worker 源码
│   ├── NapAvatarWidgetProvider.java          # 原版活动角色小组件 Provider 源码
│   ├── EnergyCalculationLogic.py             # 电量恢复时间推算、百分比及颜色状态映射逻辑
│   └── PollingStrategy.md                    # 官方刷新机制解析与 Windows 端低频拉取+本地插值策略
│
├── 04_ui_and_assets/                         # 【第四分类：视觉资产与布局结构】
│   ├── images/                               # 从 APK 资源池提取出的原版切图与图标素材
│   │   ├── icon_widget_nap_energy.png        # 绝区零核心电池/电量图标
│   │   ├── bg_widget_nap_bg.png              # 原版深色潮酷条纹背景图
│   │   ├── bg_widget_nap_default_bg.png      # 默认备用背景图
│   │   ├── ic_widget_nap_daily_active.png    # 每日活跃度状态图标
│   │   ├── widget_nap_preview_image.png      # 米游社 App 内小组件官方预览图
│   │   └── ...                               # 更多角色卡片与辅助图标
│   ├── decoded_layouts/                      # 反编译还原的标准 Android 布局 XML 文件
│   │   ├── layout_widget_nap.xml             # 小组件主界面布局
│   │   ├── layout_widget_nap_left.xml        # 左侧电量与活跃度卡片布局
│   │   ├── layout_widget_nap_right.xml       # 右侧 4 项便笺列表布局
│   │   └── ...
│   └── DesktopLayoutReference.md             # 桌面端像素级对齐与配色设计指南
│
└── 05_windows_replica_guide/                 # 【第五分类：Windows 桌面端复刻实战与登录方案】
    ├── browser_login_helper.py               # Windows 浏览器视窗登录助手 (WebView2 捕获 Cookie)
    ├── qrcode_login_helper.py                # 米游社 App 官方二维码扫码登录助手 (免输入密码)
    ├── zzz_desktop_widget_demo.py            # 开箱即用的 Windows 桌面悬浮电量小组件完整实现
    └── Architecture_Design.md                # Windows 端生产级架构设计、系统托盘与自启集成指南
```

---

## 快速上手与运行

### 1. 运行 Windows 桌面悬浮小组件 Demo
直接在当前项目目录下运行（无需配置，自动附带演示数据）：
```powershell
python extracted_zzz_widget/05_windows_replica_guide/zzz_desktop_widget_demo.py
```
- 屏幕将弹出一个绝区零风格的深色悬浮小组件；
- 鼠标按住标题栏可任意拖动位置；
- 包含当前电量、最大电量、恢复倒计时实时推算、每日活跃度与 4 项便笺状态。

### 2. 绑定真实米游社账号获取数据
提供两种登录方案：
- **方案 A（手机米游社 App 扫码登录，推荐）**：
  ```powershell
  python extracted_zzz_widget/05_windows_replica_guide/qrcode_login_helper.py
  ```
  终端将生成登录二维码，用手机米游社扫一扫确认即可自动保存凭据。
- **方案 B（桌面轻量浏览器弹窗登录）**：
  ```powershell
  python extracted_zzz_widget/05_windows_replica_guide/browser_login_helper.py
  ```
  在弹出的浏览器视窗中输入手机验证码或账号密码，登录成功后自动提取 Cookie。
- 登录成功后凭据保存在 `credentials.json`，再次启动小组件即自动在线拉取您的真实游戏电量！
