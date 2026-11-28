# -*- coding: utf-8 -*-
"""Windows 开机自启快捷方式管理（用户级启动文件夹，默认关闭，完全由用户自主选择）。"""

from __future__ import annotations

import os
import pathlib
import subprocess
import sys
from typing import Any

SHORTCUT_NAME = "MiyoQian.lnk"
LOCAL_HOSTS = ("127.0.0.1", "localhost", "::1")


def is_autostart_supported(host: str) -> bool:
    """仅在本机 Windows 桌面模式提供开机自启开关。"""
    if sys.platform != "win32":
        return False
    if str(host or "").strip() not in LOCAL_HOSTS:
        return False
    return bool(os.environ.get("APPDATA", "").strip())


def shortcut_path() -> pathlib.Path:
    appdata = os.environ.get("APPDATA", "").strip()
    return pathlib.Path(appdata) / "Microsoft" / "Windows" / "Start Menu" / "Programs" / "Startup" / SHORTCUT_NAME


def _resolve_pythonw(project_dir: pathlib.Path) -> pathlib.Path:
    candidates = [
        project_dir / ".venv" / "Scripts" / "pythonw.exe",
        pathlib.Path(sys.executable).with_name("pythonw.exe"),
        pathlib.Path(sys.executable),
    ]
    for path in candidates:
        if path.is_file():
            return path
    return pathlib.Path(sys.executable)


def get_autostart_status(project_dir: pathlib.Path, host: str) -> dict[str, Any]:
    supported = is_autostart_supported(host)
    if not supported:
        return {"supported": False, "enabled": False, "shortcut_path": ""}
    link = shortcut_path()
    return {
        "supported": True,
        "enabled": link.exists(),
        "shortcut_path": str(link),
    }


def set_autostart(project_dir: pathlib.Path, enabled: bool, host: str) -> dict[str, Any]:
    """开启或关闭当前用户的 Windows 开机后台自启快捷方式。"""
    if not is_autostart_supported(host):
        raise RuntimeError("仅本机 Windows 桌面环境支持切换开机自启")
    link = shortcut_path()
    if not enabled:
        if link.exists():
            link.unlink()
        return get_autostart_status(project_dir, host)

    link.parent.mkdir(parents=True, exist_ok=True)
    target = _resolve_pythonw(project_dir.resolve())
    env = os.environ.copy()
    env["MYQ_LINK_PATH"] = str(link)
    env["MYQ_TARGET_PATH"] = str(target)
    env["MYQ_WORKDIR"] = str(project_dir.resolve())
    script = (
        "$shell = New-Object -ComObject WScript.Shell; "
        "$sc = $shell.CreateShortcut($env:MYQ_LINK_PATH); "
        "$sc.TargetPath = $env:MYQ_TARGET_PATH; "
        "$sc.Arguments = 'main.py serve'; "
        "$sc.WorkingDirectory = $env:MYQ_WORKDIR; "
        "$sc.WindowStyle = 7; "
        "$sc.Description = 'MiyoQian Daily Checkin Service'; "
        "$sc.Save()"
    )
    result = subprocess.run(
        ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", script],
        env=env,
        capture_output=True,
        text=True,
        timeout=15,
    )
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or "创建开机自启快捷方式失败")
    return get_autostart_status(project_dir, host)
