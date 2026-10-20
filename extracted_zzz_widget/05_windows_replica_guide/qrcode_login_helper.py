# -*- coding: utf-8 -*-
"""
米哈游通行证官方二维码扫码登录助手 (QR Code Login Helper)
无需在电脑上输入密码或短信验证码，直接生成二维码，用户使用手机【米游社 App】扫一扫即可安全登录并获取 Token。
"""

import os
import sys
import json
import time
import requests
from typing import Optional, Dict

CREDENTIALS_FILE = os.path.join(os.path.dirname(__file__), "credentials.json")

# 米哈游通行证公开 App ID
APP_ID = "bll8iq97cem8"
PASSPORT_API = "https://passport-api.mihoyo.com/account/ma-cn-passport/app"


def create_qr_login() -> Optional[Dict[str, str]]:
    """创建二维码登录会话"""
    url = f"{PASSPORT_API}/createQRLogin"
    data = {"app_id": APP_ID}
    try:
        r = requests.post(url, json=data, timeout=10)
        res = r.json()
        if res.get("retcode") == 0:
            return res.get("data", {})
        else:
            print(f"[!] 创建二维码失败: {res.get('message')}")
            return None
    except Exception as e:
        print(f"[!] 请求异常: {e}")
        return None


def query_qr_status(ticket: str) -> Dict[str, any]:
    """查询二维码扫描状态"""
    url = f"{PASSPORT_API}/queryQRLoginStatus"
    data = {
        "app_id": APP_ID,
        "ticket": ticket
    }
    try:
        r = requests.post(url, json=data, timeout=10)
        return r.json()
    except Exception as e:
        return {"retcode": -1, "message": str(e)}


def print_qr_in_terminal(text: str):
    """在控制台输出二维码说明"""
    print("\n" + "="*50)
    print("【请使用手机 米游社 App 扫描下方二维码或打开链接】")
    print("二维码登录链接 (也可在手机浏览器打开或生成二维码):")
    print(text)
    print("="*50 + "\n")
    try:
        # 如果安装了 qrcode 库，可以直接在终端打印字符画二维码
        import qrcode
        qr = qrcode.QRCode()
        qr.add_data(text)
        qr.print_ascii(invert=True)
    except ImportError:
        pass


def run_qrcode_login():
    """执行二维码登录主流程"""
    print("[*] 正在向米哈游通行证服务器申请扫码登录二维码...")
    qr_data = create_qr_login()
    if not qr_data:
        return None

    ticket = qr_data.get("ticket")
    qr_url = qr_data.get("url")

    print_qr_in_terminal(qr_url)
    print("[*] 等待手机米游社 App 扫码并确认登录 (按 Ctrl+C 可中断)...")

    while True:
        status_res = query_qr_status(ticket)
        retcode = status_res.get("retcode")
        status_data = status_res.get("data", {})
        status = status_data.get("status", "")

        if status == "Created":
            sys.stdout.write(".")
            sys.stdout.flush()
        elif status == "Scanned":
            print("\n[+] 手机已扫描，请在手机米游社上点击【确认登录】...")
        elif status == "Confirmed":
            print("\n[OK] 登录确认成功！正在保存 Token...")
            tokens = status_data.get("tokens", [])
            user_info = status_data.get("user_info", {})
            uid = user_info.get("aid", "")

            cookie_dict = {
                "stuid": uid,
                "account_id": uid,
                "mid": user_info.get("mid", "")
            }
            for t in tokens:
                t_type = t.get("token_type")
                t_val = t.get("token")
                if t_type == 1:
                    cookie_dict["stoken"] = t_val
                elif t_type == 2:
                    cookie_dict["ltoken"] = t_val
                elif t_type == 4:
                    cookie_dict["cookie_token"] = t_val

            raw_cookie = "; ".join([f"{k}={v}" for k, v in cookie_dict.items()])
            result = {
                "updated_at": int(time.time()),
                "uid": uid,
                "stuid": cookie_dict.get("stuid", ""),
                "stoken": cookie_dict.get("stoken", ""),
                "mid": cookie_dict.get("mid", ""),
                "account_id": uid,
                "cookie_token": cookie_dict.get("cookie_token", ""),
                "raw_cookie": raw_cookie
            }
            with open(CREDENTIALS_FILE, "w", encoding="utf-8") as f:
                json.dump(result, f, ensure_ascii=False, indent=2)

            print(f"[OK] 凭据已成功写入: {CREDENTIALS_FILE}")
            return result
        elif status == "Expired":
            print("\n[!] 二维码已过期，请重新运行登录助手。")
            return None
        else:
            if retcode != 0:
                print(f"\n[!] 状态异常: {status_res.get('message')}")
                return None

        time.sleep(2)


if __name__ == "__main__":
    run_qrcode_login()
