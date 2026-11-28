# -*- coding: utf-8 -*-
"""本机浏览器弹窗登录并自动捕获凭证（CDP）。"""

from __future__ import annotations

import json
import os
import pathlib
import shutil
import socket
import subprocess
import tempfile
import threading
import time
from typing import Callable
from urllib.parse import urlparse

import httpx
from websockets.exceptions import ConnectionClosed
from websockets.sync.client import connect as ws_connect

from ..overseas.cookie import parse_hoyolab_cookie

TARGET_URLS = {
    "cloud_genshin": "https://ys.mihoyo.com/cloud/#/",
    "hoyolab": "https://www.hoyolab.com/",
}


def find_browser_executable() -> str | None:
    """查找本机可用的 Edge 或 Chrome 浏览器路径。"""
    candidates: list[str] = []
    if os.name == "nt":
        for env_key in ("PROGRAMFILES(X86)", "PROGRAMFILES", "LOCALAPPDATA"):
            base = os.environ.get(env_key)
            if not base:
                continue
            candidates.extend(
                [
                    str(pathlib.Path(base) / "Microsoft/Edge/Application/msedge.exe"),
                    str(pathlib.Path(base) / "Google/Chrome/Application/chrome.exe"),
                ]
            )
    for name in ("msedge", "microsoft-edge", "chrome", "google-chrome", "chromium"):
        resolved = shutil.which(name)
        if resolved:
            candidates.append(resolved)
    for path in candidates:
        if path and pathlib.Path(path).is_file():
            return path
    return None


def _find_free_port() -> int:
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.bind(("127.0.0.1", 0))
        return int(sock.getsockname()[1])


def _wait_cdp_ws_url(
    port: int,
    proc: subprocess.Popen[bytes],
    deadline: float,
    cancel_event: threading.Event | None,
) -> str:
    endpoint = f"http://127.0.0.1:{port}/json/list"
    while time.monotonic() < deadline:
        if cancel_event and cancel_event.is_set():
            raise RuntimeError("捕获已取消")
        if proc.poll() is not None:
            raise RuntimeError("登录窗口已关闭，捕获已取消")
        try:
            resp = httpx.get(endpoint, timeout=1.5, trust_env=False)
            if resp.status_code == 200:
                for item in resp.json():
                    if item.get("type") == "page" and item.get("webSocketDebuggerUrl"):
                        return str(item["webSocketDebuggerUrl"])
        except Exception:
            pass
        time.sleep(0.3)
    raise RuntimeError("等待浏览器调试端口响应超时")


def _extract_combo_token(params: dict) -> str:
    request = params.get("request") or {}
    host = (urlparse(str(request.get("url") or "")).hostname or "").lower()
    if not (
        host == "mihoyo.com"
        or host.endswith(".mihoyo.com")
        or host == "hoyoverse.com"
        or host.endswith(".hoyoverse.com")
    ):
        return ""
    headers = request.get("headers") or {}
    if not isinstance(headers, dict):
        return ""
    for key, value in headers.items():
        if str(key).lower() == "x-rpc-combo_token":
            token = str(value or "").strip()
            if token:
                return token
    return ""


def _build_hoyolab_cookie(cookies_list: list[dict]) -> str:
    pairs: dict[str, str] = {}
    for item in cookies_list:
        if not isinstance(item, dict):
            continue
        domain = str(item.get("domain") or "").lower().lstrip(".")
        if not (domain == "hoyolab.com" or domain.endswith(".hoyolab.com")):
            continue
        name = str(item.get("name") or "").strip()
        value = str(item.get("value") or "").strip()
        if name and value:
            pairs[name] = value
    uid = pairs.get("ltuid_v2") or pairs.get("ltuid") or ""
    token = pairs.get("ltoken_v2") or pairs.get("ltoken") or ""
    if not uid or not token:
        return ""
    uid_key = "ltuid_v2" if "ltuid_v2" in pairs else "ltuid"
    token_key = "ltoken_v2" if "ltoken_v2" in pairs else "ltoken"
    candidate = f"{uid_key}={uid}; {token_key}={token}"
    parsed = parse_hoyolab_cookie(candidate)
    return parsed["cookie"]


def capture_browser_credentials(
    target: str,
    timeout: int = 180,
    cancel_event: threading.Event | None = None,
    on_status: Callable[[str], None] | None = None,
) -> dict[str, str]:
    """打开干净浏览器窗口引导用户登录官网并自动提取对应凭证。"""
    if target not in TARGET_URLS:
        raise ValueError(f"不支持的捕获目标: {target}")
    browser = find_browser_executable()
    if not browser:
        raise RuntimeError("未找到本机 Edge 或 Chrome 浏览器，请改用手动粘贴")

    port = _find_free_port()
    temp_dir = tempfile.mkdtemp(prefix="myq_browser_")
    proc: subprocess.Popen[bytes] | None = None
    try:
        args = [
            browser,
            f"--app={TARGET_URLS[target]}",
            f"--remote-debugging-port={port}",
            f"--user-data-dir={temp_dir}",
            "--no-first-run",
            "--no-default-browser-check",
            "--window-size=1080,760",
        ]
        proc = subprocess.Popen(args, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        deadline = time.monotonic() + max(timeout, 30)
        if on_status:
            on_status("正在打开登录窗口")
        ws_url = _wait_cdp_ws_url(port, proc, deadline, cancel_event)
        if on_status:
            tip = (
                "请在弹出的云原神窗口中登录账号（登录后如未触发可点右上角时长/钱包）"
                if target == "cloud_genshin"
                else "请在弹出的 HoYoLAB 窗口中完成登录，登录后将自动提取 Cookie"
            )
            on_status(tip)

        with ws_connect(ws_url, open_timeout=5, proxy=None) as ws:
            msg_id = 1
            ws.send(json.dumps({"id": msg_id, "method": "Network.enable"}))
            last_cookie_poll = 0.0
            cookie_req_id = 0

            while time.monotonic() < deadline:
                if cancel_event and cancel_event.is_set():
                    raise RuntimeError("捕获已取消")
                if proc.poll() is not None:
                    raise RuntimeError("登录窗口已关闭，捕获已取消")

                now = time.monotonic()
                if target == "hoyolab" and now - last_cookie_poll >= 1.0:
                    msg_id += 1
                    cookie_req_id = msg_id
                    ws.send(json.dumps({"id": cookie_req_id, "method": "Network.getAllCookies"}))
                    last_cookie_poll = now

                try:
                    raw = ws.recv(timeout=0.5)
                except TimeoutError:
                    continue
                except ConnectionClosed:
                    if cancel_event and cancel_event.is_set():
                        raise RuntimeError("捕获已取消")
                    if proc.poll() is not None:
                        raise RuntimeError("登录窗口已关闭，捕获已取消")
                    raise RuntimeError("浏览器调试连接已断开")

                data = json.loads(raw)
                if target == "cloud_genshin" and data.get("method") == "Network.requestWillBeSent":
                    token = _extract_combo_token(data.get("params") or {})
                    if token:
                        return {"combo_token": token}
                elif target == "hoyolab" and data.get("id") == cookie_req_id:
                    cookies_list = ((data.get("result") or {}).get("cookies")) or []
                    cookie = _build_hoyolab_cookie(cookies_list)
                    if cookie:
                        return {"cookie": cookie}

        raise RuntimeError("等待登录超时，请重试")
    finally:
        if proc is not None and proc.poll() is None:
            proc.terminate()
            try:
                proc.wait(timeout=3)
            except subprocess.TimeoutExpired:
                proc.kill()
                try:
                    proc.wait(timeout=3)
                except subprocess.TimeoutExpired:
                    pass
        shutil.rmtree(temp_dir, ignore_errors=True)
