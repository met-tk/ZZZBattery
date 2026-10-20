# 米游社客户端请求头 (Headers) 与校验规范

在米游社 Android 客户端逆向过程中，从 `classes2.dex` 中的 `in.C1712u`（LoginStatusInterceptor）、`in.C1704m`（DsInterceptor）以及 `Ce.C0151l0`（NetworkUtils）中提取出关键的请求头规范。

---

## 一、 请求头清单与说明

当向 `https://api-takumi-record.mihoyo.com` 发起小组件数据请求时，必须附带以下 Headers：

| Header 键名 | 示例值 | 说明 |
| :--- | :--- | :--- |
| `x-rpc-client_type` | `2` (Android) 或 `5` (Desktop) | 客户端类型：`1`=iOS, `2`=Android, `5`=PC/Web |
| `x-rpc-app_version` | `2.114.0` | 米游社客户端版本号（与 APK 内保持一致） |
| `x-rpc-sys_version` | `14` | 操作系统版本 |
| `x-rpc-channel` | `mihoyo` | 渠道标识 |
| `x-rpc-device_id` | `UUID.randomUUID()` | 客户端设备唯一标识符（GUID） |
| `x-rpc-device_name` | `Xiaomi 23049RAD8C` | 设备型号名 |
| `x-rpc-device_model`| `23049RAD8C` | 设备具体 Model |
| `x-rpc-device_fp` | `38d7...` (可固定或随机) | 设备指纹 |
| `DS` | `1729312345,abcdef,0123...` | 核心动态签名（Dynamic Secret，详见 DS 算法实现） |
| `Referer` | `https://app.mihoyo.com` | 米游社客户端 Referer |
| `User-Agent` | `Mozilla/5.0 ... miHoYoBBS/2.114.0` | 客户端 User-Agent |
| `Cookie` | `stuid=...; stoken=...; mid=...;` | 用户认证 Token (在 Web 端可用 `account_id` 与 `cookie_token`) |

---

## 二、 认证 Cookie 格式差异

米哈游通行证体系有两种主要的 Token 组合：

### 1. 移动端 App / 小组件常用（Stoken 体系）
```http
Cookie: stuid=<用户米哈游通行证ID>; stoken=<用户stoken>; mid=<用户mid>;
```
- 特点：权限完整，支持桌面小组件、一键签到、游戏数据获取等所有 API。

### 2. 网页端 / 浏览器登录常用（Ltoken / CookieToken 体系）
```http
Cookie: account_id=<用户米哈游通行证ID>; cookie_token=<用户cookie_token>; ltuid=<用户ID>; ltoken=<用户ltoken>;
```
- 特点：在浏览器（WebView2）中输入验证码或扫码后，浏览器会自动存储这些 Cookie。
- 在 Windows 端开发时，拿到网页端 Cookie 后，既可直接请求部分公开的 Web 端便笺接口，也可以调用官方换票接口将 `cookie_token` 无缝换取 `stoken`。

---

## 三、 动态防刷与错误响应码 (RetCode) 处理

从 `NapWidgetWorker.java` 的 `T` 方法（错误提示构建函数）及 `Y` 方法中提取出的官方错误码处理机制：

| 错误码 (RetCode) | 含义 | 官方小组件 UI 提示 | Windows 桌面小组件处理策略 |
| :--- | :--- | :--- | :--- |
| `0` | 请求成功 | 正常渲染电量与便笺 | 刷新 UI 并记录最后成功获取时间 |
| `-100` | 登录失效 / 未登录 | "绳匠还没有登录，点击重新登录" | 弹出桌面登录窗口或提醒重新配置 Cookie |
| `10110` | 未绑定游戏角色 | "没有查询到角色，点击进入米游社查看战绩" | 提示用户先在绝区零中创建角色并进入游戏 |
| `1034` / `10035` | 触发风控人机验证 (Geetest) | 请求受限 | 请求头中加入有效设备指纹或降低轮询频率 |
| `-999` / 网络异常 | 网络请求超时或断网 | "数据加载失败，点击刷新" | 保留上次成功的缓存数据，稍后重试 |
