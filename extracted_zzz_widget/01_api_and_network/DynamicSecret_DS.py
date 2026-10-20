# -*- coding: utf-8 -*-
"""
米游社动态密钥 (Dynamic Secret, DS) 生成算法
提取自米游社客户端安全拦截层与社区标准化协议，用于在 Windows 平台上脱离 Android 动态库独立签名。

DS 生成格式：
  DS = f"{timestamp},{random_str},{md5_hash}"
"""

import time
import random
import string
import hashlib
from typing import Optional, Dict, Any
from urllib.parse import urlencode


# 常用米游社客户端 Salt 常量定义
# 注：米游社不同 API 端点对应不同版本的 Salt
SALT_K2 = "dWCcD2FsOUzgLEkeavjngdGlfrbxF6bv"        # DS v1 常用 salt (Web / Android)
SALT_LK2 = "xV8v9Qu54cheAnchZzncxctdaY04BJfd"       # DS v1 takumi salt
SALT_4X = "xV8v9Qu54cheAnchZzncxctdaY04BJfd"        # DS v2 基础查询 salt
SALT_PROD_RECORD = "t0qEgfub6cvueAPgR5mMgQFtTrJEOKpK" # 米游社战绩 / 实时便笺专用 Salt (2.x 最新体系)


def _get_random_str(length: int = 6) -> str:
    """生成指定长度的随机英文字母和数字字符串"""
    chars = string.ascii_letters + string.digits
    return ''.join(random.choice(chars) for _ in range(length))


def generate_ds_v1(salt: str = SALT_K2) -> str:
    """
    生成 DS v1 签名（用于无参或简单 GET 请求）
    规则：
      t: 当前时间戳（10位秒级）
      r: 6位随机字符串
      c: md5(f"salt={salt}&t={t}&r={r}")
    """
    t = int(time.time())
    r = _get_random_str(6)
    raw = f"salt={salt}&t={t}&r={r}"
    c = hashlib.md5(raw.encode('utf-8')).hexdigest()
    return f"{t},{r},{c}"


def generate_ds_v2(query: Optional[str] = None, 
                   body: Optional[str] = None, 
                   salt: str = SALT_PROD_RECORD) -> str:
    """
    生成 DS v2 签名（用于米游社战绩/便笺接口与带参数的请求）
    规则：
      t: 当前时间戳（10位秒级）
      r: 100000~200000 之间的随机数或6位随机字符
      b: POST 请求的 JSON Body 字符串（若为空则为 ""）
      q: GET 请求的 URL Query 字符串（按键名 ASCII 升序排序，若为空则为 ""）
      c: md5(f"salt={salt}&t={t}&r={r}&b={b}&q={q}")
    """
    t = int(time.time())
    r = str(random.randint(100001, 200000))
    b = body if body else ""
    q = query if query else ""
    
    raw = f"salt={salt}&t={t}&r={r}&b={b}&q={q}"
    c = hashlib.md5(raw.encode('utf-8')).hexdigest()
    return f"{t},{r},{c}"


def get_zzz_widget_ds(version: str = "WV_Default") -> str:
    """
    专门为绝区零小组件接口生成的 DS 签名
    对应请求: GET /event/game_record_zzz/api/zzz/widget?version=WV_Default
    """
    query = f"version={version}"
    return generate_ds_v2(query=query, body="", salt=SALT_PROD_RECORD)


if __name__ == "__main__":
    print("=== 测试 DS 生成 ===")
    ds1 = generate_ds_v1()
    print("DS v1:", ds1)
    ds2 = get_zzz_widget_ds("WV_Default")
    print("DS v2 (ZZZ Widget):", ds2)
