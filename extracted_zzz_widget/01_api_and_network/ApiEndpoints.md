# 绝区零 (ZZZ) 米游社桌面小组件 API 接口逆向与规范文档

在逆向分析 `米游社.apk`（版本：`2.114.0`）的 `classes12.dex` 与 `classes2.dex` 之后，我们提取到了米游社官方 Android 桌面电量与活动小组件所依赖的核心后端网络接口。

---

## 一、 核心小组件接口 (Widget API)

该接口由 `com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetWorker` 发起请求，专门为桌面小组件设计，一次性下发小组件展示所需的所有聚合数据（包含电量、电量恢复秒数、签到状态、活跃度、调查点便笺列表、启动跳转链接等）。

### 1. 基础信息
- **请求方法**：`GET`
- **请求基础域名 (Base URL)**：`https://api-takumi-record.mihoyo.com`
- **请求路径 (Endpoint)**：`/event/game_record_zzz/api/zzz/widget`
- **对应 Retrofit 接口**：`com.mihoyo.hyperion.biz.miniwidget.nap.a.java`

### 2. Query 参数
| 参数名 | 类型 | 必填 | 默认值 / 示例值 | 说明 |
| :--- | :--- | :--- | :--- | :--- |
| `version` | string | 是 | `WV_Default` (普通电量小组件)<br>`WV_V2` (带角色活动的二代小组件) | 小组件数据版本控制标识 |

### 3. 请求 Headers (关键认证)
```http
GET /event/game_record_zzz/api/zzz/widget?version=WV_Default HTTP/1.1
Host: api-takumi-record.mihoyo.com
x-rpc-client_type: 2
x-rpc-app_version: 2.114.0
x-rpc-sys_version: 14
x-rpc-channel: mihoyo
x-rpc-device_id: <随机UUID或设备ID>
x-rpc-device_fp: <设备指纹>
DS: <动态签名，由 Salt 生成>
Referer: https://app.mihoyo.com
Cookie: stuid=<用户米哈游通行证ID>; stoken=<用户stoken>; mid=<用户mid>;
```

### 4. 响应 JSON 结构实体
```json
{
  "retcode": 0,
  "message": "OK",
  "data": {
    "energy": {
      "progress": {
        "current": 240,
        "max": 240
      },
      "restore": 0
    },
    "vitality": {
      "current": 400,
      "max": 400
    },
    "has_signed": true,
    "sign_url": "https://act.mihoyo.com/bbs/event/signin/zzz/index.html?act_id=e202406242138391",
    "note_url": "https://webstatic.mihoyo.com/app/community-game-records/index.html?game_id=8#/zzz/note",
    "home_url": "mihoyobbs://homeForum?game_id=8",
    "main_home_url": "mihoyobbs://homeForum?game_id=8",
    "note_list": [
      {
        "name": "刮刮卡",
        "name_color": "A6FFFFFF",
        "value": "已刮开",
        "value_color": "E5FFFFFF"
      },
      {
        "name": "录像店营收",
        "name_color": "A6FFFFFF",
        "value": "待营业",
        "value_color": "FFFFC529"
      },
      {
        "name": "零号空洞调查点数",
        "name_color": "A6FFFFFF",
        "value": "8000/8000",
        "value_color": "E5FFFFFF"
      },
      {
        "name": "喝咖啡",
        "name_color": "A6FFFFFF",
        "value": "已享用",
        "value_color": "E5FFFFFF"
      }
    ],
    "hoyo_link_cn": {
      "deeplink": "nap://launch",
      "deeplink_cloud": "mihoyocloudnap://launch",
      "applink": "https://nap.mihoyo.com"
    }
  }
}
```

---

## 二、 角色背景图列表接口 (Avatar Background API)

该接口由 `NapAvatarWidgetWorker` 调用，用于获取绝区零小组件可选的背景壁纸（通常包含当期 UP 角色、主角兄妹、狡兔屋等立绘背景图）。

- **请求方法**：`GET`
- **请求 URL**：`https://api-takumi-record.mihoyo.com/event/game_record_zzz/api/zzz/get_widget_avatar_background`
- **参数**：无 Query 参数
- **响应示例**：
```json
{
  "retcode": 0,
  "message": "OK",
  "data": {
    "background_list": [
      {
        "id": 1,
        "name": "铃",
        "bg_url": "https://upload-bbs.mihoyo.com/game_record/zzz/widget_bg_belle.png",
        "tab_icon_selected": "https://upload-bbs.mihoyo.com/game_record/zzz/tab_belle_sel.png",
        "tab_icon_unselected": "https://upload-bbs.mihoyo.com/game_record/zzz/tab_belle_unsel.png"
      }
    ]
  }
}
```

---

## 三、 用户游戏角色列表查询接口 (User Game Roles API)

在拉取便笺或进行针对性游戏数据查询前，用于确认当前已登录账号下绑定的绝区零 UID、区服（Server）与角色等级。

- **请求方法**：`GET`
- **请求 URL**：`https://api-takumi.mihoyo.com/binding/api/getUserGameRolesByCookie?game_biz=nap_cn`
  *(或使用 stoken: `https://api-takumi.mihoyo.com/binding/api/getUserGameRolesByStoken?game_biz=nap_cn`)*
- **Query 参数**：
  - `game_biz`: `nap_cn`（绝区零国内官方服/B服；国际服为 `nap_global`）
- **响应结构**：
```json
{
  "retcode": 0,
  "message": "OK",
  "data": {
    "list": [
      {
        "game_biz": "nap_cn",
        "region": "prod_gf_cn",
        "game_uid": "100012345",
        "nickname": "绳匠昵称",
        "level": 55,
        "is_chosen": true,
        "region_name": "官方服务器",
        "is_official": true
      }
    ]
  }
}
```

---

## 四、 完整实时便笺详细接口 (Daily Note API，桌面扩展备选)

若 Windows 桌面小挂件希望显示更深度、细节更多的游戏数据（如先驱探索备用电量存储上限、周本击败次数、零号空洞悬赏等），可选用标准战绩实时便笺接口：

- **请求方法**：`GET`
- **请求 URL**：`https://api-takumi-record.mihoyo.com/game_record/app/zzz/api/daily_note`
- **Query 参数**：
  - `role_id`: 游戏 UID（如 `100012345`）
  - `server`: 所在区服（如 `prod_gf_cn`）
- **返回主要字段**：
  - `energy.progress.current` / `max`：当前电量与上限
  - `energy.restore`：完全恢复剩余秒数
  - `vitality.current` / `max`：每日活跃度 (0 ~ 400)
  - `vhs_sale.sale_state`：录像店经营状态 (`SaleStateDoing`, `SaleStateDone`, `SaleStateNo`)
  - `card_sign.has_signed`：刮刮卡今日是否已刮
  - `coffee.coffee_state`：今日咖啡是否已喝 (`CoffeeStateDone`, `CoffeeStateWait`)
  - `survey_points.current` / `max`：零号空洞每周调查点数
