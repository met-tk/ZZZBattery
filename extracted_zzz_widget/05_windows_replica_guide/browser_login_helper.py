# -*- coding: utf-8 -*-
"""
Windows 桌面端米游社浏览器登录助手 (Browser Login Helper)
基于 Windows 平台的原生 WebView2 (pywebview) 构建。
用户在一个轻量级浏览器弹窗中打开米游社官方登录页面，登录成功后自动捕获并持久化 Cookie/Token。
"""

import sys
import os
import json
import time
from typing import Dict, Optional

CREDENTIALS_FILE = os.path.join(os.path.dirname(__file__), "credentials.json")


def save_credentials(cookie_dict: Dict[str, str], uid: Optional[str] = None):
    """保存提取到的用户认证信息到本地配置文件"""
    data = {
        "updated_at": int(time.time()),
        "uid": uid or cookie_dict.get("account_id") or cookie_dict.get("stuid") or cookie_dict.get("ltuid", ""),
        "stuid": cookie_dict.get("stuid", ""),
        "stoken": cookie_dict.get("stoken", ""),
        "mid": cookie_dict.get("mid", ""),
        "account_id": cookie_dict.get("account_id", ""),
        "cookie_token": cookie_dict.get("cookie_token", ""),
        "ltuid": cookie_dict.get("ltuid", ""),
        "ltoken": cookie_dict.get("ltoken", ""),
        "raw_cookie": "; ".join([f"{k}={v}" for k, v in cookie_dict.items()])
    }
    with open(CREDENTIALS_FILE, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    print(f"\n[OK] 登录凭据已成功保存至: {CREDENTIALS_FILE}")
    return data


def load_credentials() -> Optional[Dict[str, str]]:
    """读取本地已保存的凭据"""
    if os.path.exists(CREDENTIALS_FILE):
        try:
            with open(CREDENTIALS_FILE, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            return None
    return None


def login_with_webview():
    """
    通过 pywebview (基于 Windows Edge/WebView2 内核) 弹出米游社官方登录页面
    """
    try:
        import webview
    except ImportError:
        print("[!] 未检测到 pywebview，请在命令行中运行: pip install pywebview")
        return None

    # 米游社移动端登录页（简洁、适合小窗体，支持短信验证码与账号密码登录）
    login_url = "https://m.bbs.mihoyo.com/ys/#/login"
    captured_cookies = {}

    def on_loaded(window):
        nonlocal captured_cookies
        # 周期性检查 Cookie 中是否出现了登录成功的核心字段
        cookies = window.get_cookies()
        c_dict = {}
        for c in cookies:
            for k, v in c.items():
                c_dict[k] = v.value if hasattr(v, 'value') else str(v)

        # 检查是否包含核心身份标识
        if any(k in c_dict for k in ["account_id", "stuid", "ltuid"]):
            print("[+] 检测到登录成功标志！正在提取 Token...")
            captured_cookies = c_dict
            window.destroy()

    print("[*] 正在启动米游社登录视窗，请在弹出的窗口中登录您的账号...")
    window = webview.create_window(
        title="米游社账号登录 - 绝区零小组件助手",
        url=login_url,
        width=420,
        height=650,
        resizable=False,
        confirm_close=False
    )

    # 监听页面加载或关闭
    window.events.loaded += on_loaded
    webview.start()

    if captured_cookies:
        return save_credentials(captured_cookies)
    else:
        print("[!] 窗口已关闭，未提取到有效登录 Cookie。")
        return None


def manual_cookie_input():
    """
    备用方案：手动粘贴 Cookie 字符串
    """
    print("\n=== 手动输入米游社 Cookie ===")
    print("您可以直接在浏览器打开 https://www.miyoushe.com 登录后，F12 控制台输入 document.cookie 复制粘贴：")
    raw = input("请粘贴完整 Cookie 字符串: ").strip()
    if not raw:
        return None
    
    parts = raw.split(";")
    c_dict = {}
    for p in parts:
        if "=" in p:
            k, v = p.strip().split("=", 1)
            c_dict[k.strip()] = v.strip()
            
    return save_credentials(c_dict)


if __name__ == "__main__":
    print("=== 绝区零桌面端米游社登录助手 ===")
    print("1. 启动轻量级浏览器窗口登录 (推荐)")
    print("2. 手动粘贴 Cookie")
    choice = input("请选择登录方式 (默认 1): ").strip()
    if choice == "2":
        manual_cookie_input()
    else:
        login_with_webview()
