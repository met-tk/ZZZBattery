# -*- coding: utf-8 -*-
"""
绝区零 (ZZZ) Windows 桌面悬浮电量小组件 Demo
基于 Tkinter (Python 原生内置，免额外安装依赖，即开即用) 实现。

功能特性：
1. 像素级还原米游社官方 Android 桌面绝区零便笺小组件 UI；
2. 无边框潮酷深色/CRT风格窗口，支持鼠标任意拖拽移动；
3. 支持电量 (Energy) 与恢复时间实时倒计时插值；
4. 支持便笺条目 (录像店、刮刮卡、零号空洞调查点、喝咖啡) 状态展示；
5. 若存在本地 credentials.json 则自动在线同步，无凭据时自动进入演示 Demo 模式；
6. 支持一键快捷刷新与一键呼出登录助手。
"""

import os
import sys
import json
import time
import threading
import tkinter as tk
from tkinter import ttk, messagebox
import requests

# 引入本模块内的辅助组件
CUR_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT_DIR = os.path.dirname(CUR_DIR)
sys.path.insert(0, os.path.join(ROOT_DIR, "01_api_and_network"))
sys.path.insert(0, os.path.join(ROOT_DIR, "02_data_models"))
sys.path.insert(0, os.path.join(ROOT_DIR, "03_business_logic"))

from DynamicSecret_DS import get_zzz_widget_ds
from NapWidgetDataModel import NapWidgetData, EnergyProgress
from EnergyCalculationLogic import EnergyCalculationLogic

try:
    from PIL import Image, ImageTk
    HAS_PIL = True
except ImportError:
    HAS_PIL = False

CREDENTIALS_FILE = os.path.join(CUR_DIR, "credentials.json")
ASSETS_DIR = os.path.join(ROOT_DIR, "04_ui_and_assets", "images")


class ZzzDesktopWidget(tk.Tk):
    def __init__(self):
        super().__init__()

        self.title("绝区零 - 桌面便笺小组件")
        self.geometry("380x190+100+100")
        self.overrideredirect(True)  # 无边框窗口
        self.attributes("-topmost", True)  # 默认置顶悬浮
        self.configure(bg="#121316")

        # 拖拽窗口相关变量
        self._drag_start_x = 0
        self._drag_start_y = 0

        # 数据状态
        self.widget_data = None
        self.is_online = False
        self.last_sync_time = 0

        # 初始化 UI
        self._init_ui()
        self._bind_mouse_events()

        # 启动本地每秒插值倒计时时钟
        self.after(1000, self._on_local_second_tick)

        # 初始拉取或加载 Demo 数据
        self.after(100, self.refresh_data)

    def _init_ui(self):
        """构建复古潮酷风格小组件 UI"""
        # 主容器（带细微描边效果）
        self.main_frame = tk.Frame(self, bg="#181A1F", highlightbackground="#2E323B", highlightthickness=1)
        self.main_frame.pack(fill=tk.BOTH, expand=True, padx=2, pady=2)

        # 顶部标题栏
        self.header_frame = tk.Frame(self.main_frame, bg="#181A1F")
        self.header_frame.pack(fill=tk.X, padx=12, pady=(8, 4))

        self.title_label = tk.Label(
            self.header_frame,
            text="● 绝区零 战绩便笺",
            font=("Segoe UI", 9, "bold"),
            fg="#F0F0F0",
            bg="#181A1F"
        )
        self.title_label.pack(side=tk.LEFT)

        # 顶部右侧按钮
        self.btn_close = tk.Label(
            self.header_frame,
            text="✕",
            font=("Segoe UI", 9, "bold"),
            fg="#888888",
            bg="#181A1F",
            cursor="hand2"
        )
        self.btn_close.pack(side=tk.RIGHT, padx=(6, 0))
        self.btn_close.bind("<Button-1>", lambda e: self.destroy())

        self.btn_refresh = tk.Label(
            self.header_frame,
            text="刷新",
            font=("Segoe UI", 8),
            fg="#CCCCCC",
            bg="#22252C",
            padx=6,
            pady=1,
            cursor="hand2"
        )
        self.btn_refresh.pack(side=tk.RIGHT, padx=4)
        self.btn_refresh.bind("<Button-1>", lambda e: self.refresh_data())

        self.btn_login = tk.Label(
            self.header_frame,
            text="账号登录",
            font=("Segoe UI", 8),
            fg="#FFC529",
            bg="#22252C",
            padx=6,
            pady=1,
            cursor="hand2"
        )
        self.btn_login.pack(side=tk.RIGHT, padx=4)
        self.btn_login.bind("<Button-1>", lambda e: self._open_login())

        # 中部内容容器：左侧电量，右侧便笺
        self.content_frame = tk.Frame(self.main_frame, bg="#181A1F")
        self.content_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=4)

        # ====== 左侧核心电量卡片 ======
        self.left_card = tk.Frame(self.content_frame, bg="#1F232B", padx=8, pady=6)
        self.left_card.pack(side=tk.LEFT, fill=tk.BOTH, expand=True, padx=(0, 4))

        self.energy_title_frame = tk.Frame(self.left_card, bg="#1F232B")
        self.energy_title_frame.pack(fill=tk.X)

        self.energy_icon_img = None
        energy_icon_path = os.path.join(ASSETS_DIR, "icon_widget_nap_energy.png")
        if HAS_PIL and os.path.exists(energy_icon_path):
            try:
                img = Image.open(energy_icon_path).resize((26, 26), Image.Resampling.LANCZOS)
                self.energy_icon_img = ImageTk.PhotoImage(img)
            except Exception:
                pass

        if self.energy_icon_img:
            self.energy_icon_label = tk.Label(
                self.energy_title_frame,
                image=self.energy_icon_img,
                bg="#1F232B"
            )
        else:
            self.energy_icon_label = tk.Label(
                self.energy_title_frame,
                text="⚡",
                font=("Segoe UI", 14, "bold"),
                fg="#FFC529",
                bg="#1F232B"
            )
        self.energy_icon_label.pack(side=tk.LEFT)

        self.energy_val_label = tk.Label(
            self.energy_title_frame,
            text="---",
            font=("Segoe UI", 16, "bold"),
            fg="#FFFFFF",
            bg="#1F232B"
        )
        self.energy_val_label.pack(side=tk.LEFT, padx=(4, 0))

        self.energy_total_label = tk.Label(
            self.energy_title_frame,
            text="/240",
            font=("Segoe UI", 10),
            fg="#888888",
            bg="#1F232B"
        )
        self.energy_total_label.pack(side=tk.LEFT, padx=(1, 0), pady=(6, 0))

        # 恢复时间
        self.restore_label = tk.Label(
            self.left_card,
            text="恢复计算中...",
            font=("Segoe UI", 8),
            fg="#B0B0B0",
            bg="#1F232B"
        )
        self.restore_label.pack(anchor="w", pady=(2, 6))

        # 活跃度区域
        self.vitality_frame = tk.Frame(self.left_card, bg="#16181E", padx=4, pady=3)
        self.vitality_frame.pack(fill=tk.X, side=tk.BOTTOM)

        self.vitality_label = tk.Label(
            self.vitality_frame,
            text="今日活跃: --/400",
            font=("Segoe UI", 8),
            fg="#CCCCCC",
            bg="#16181E"
        )
        self.vitality_label.pack(side=tk.LEFT)

        # ====== 右侧便笺条目卡片 ======
        self.right_card = tk.Frame(self.content_frame, bg="#1F232B", padx=8, pady=6)
        self.right_card.pack(side=tk.RIGHT, fill=tk.BOTH, expand=True, padx=(4, 0))

        self.note_items_labels = []
        for i in range(4):
            item_frame = tk.Frame(self.right_card, bg="#1F232B")
            item_frame.pack(fill=tk.X, pady=2)

            name_lbl = tk.Label(item_frame, text=f"条目 {i+1}", font=("Segoe UI", 8), fg="#CCCCCC", bg="#1F232B")
            name_lbl.pack(side=tk.LEFT)

            val_lbl = tk.Label(item_frame, text="--", font=("Segoe UI", 8, "bold"), fg="#FFFFFF", bg="#1F232B")
            val_lbl.pack(side=tk.RIGHT)

            self.note_items_labels.append((name_lbl, val_lbl))

        # 底部状态栏
        self.status_bar = tk.Label(
            self.main_frame,
            text="状态: 初始化中...",
            font=("Segoe UI", 7),
            fg="#5C6370",
            bg="#181A1F",
            anchor="w"
        )
        self.status_bar.pack(fill=tk.X, side=tk.BOTTOM, padx=12, pady=(0, 4))

    def _bind_mouse_events(self):
        """支持鼠标任意按住标题栏拖动窗口"""
        def on_press(event):
            self._drag_start_x = event.x
            self._drag_start_y = event.y

        def on_motion(event):
            x = self.winfo_x() + (event.x - self._drag_start_x)
            y = self.winfo_y() + (event.y - self._drag_start_y)
            self.geometry(f"+{x}+{y}")

        self.main_frame.bind("<ButtonPress-1>", on_press)
        self.main_frame.bind("<B1-Motion>", on_motion)
        self.header_frame.bind("<ButtonPress-1>", on_press)
        self.header_frame.bind("<B1-Motion>", on_motion)
        self.title_label.bind("<ButtonPress-1>", on_press)
        self.title_label.bind("<B1-Motion>", on_motion)

    def _open_login(self):
        """弹出登录方式选择或直接运行扫码登录"""
        from qrcode_login_helper import run_qrcode_login
        threading.Thread(target=run_qrcode_login, daemon=True).start()
        messagebox.showinfo("扫码登录", "已启动米哈游通行证扫码登录！请查看控制台输出并使用米游社 App 扫码。")

    def _on_local_second_tick(self):
        """每秒本地插值时钟，实现流畅的秒级倒计时，无需频繁调用 API"""
        if self.widget_data and self.widget_data.energy:
            e = self.widget_data.energy
            if e.restore > 0:
                e.restore -= 1
                # 检查是否刚好跨过 360 秒刻度恢复 1 点体力
                if e.restore % 360 == 0:
                    e.progress.current = min(e.progress.max, e.progress.current + 1)
                self.restore_label.config(text=e.formatted_restore_time)
                self.energy_val_label.config(text=str(e.progress.current))
            else:
                self.restore_label.config(text="已全部恢复")

        self.after(1000, self._on_local_second_tick)

    def refresh_data(self):
        """触发数据刷新（异步线程执行）"""
        self.status_bar.config(text="正在同步米游社最新战绩...")
        threading.Thread(target=self._fetch_data_worker, daemon=True).start()

    def _fetch_data_worker(self):
        """后台拉取网络数据或加载 Demo"""
        if os.path.exists(CREDENTIALS_FILE):
            try:
                with open(CREDENTIALS_FILE, "r", encoding="utf-8") as f:
                    creds = json.load(f)
                
                # 发起真实网络请求
                url = "https://api-takumi-record.mihoyo.com/event/game_record_zzz/api/zzz/widget?version=WV_Default"
                headers = {
                    "x-rpc-client_type": "2",
                    "x-rpc-app_version": "2.114.0",
                    "x-rpc-sys_version": "14",
                    "x-rpc-channel": "mihoyo",
                    "DS": get_zzz_widget_ds("WV_Default"),
                    "Referer": "https://app.mihoyo.com",
                    "Cookie": creds.get("raw_cookie", "")
                }
                r = requests.get(url, headers=headers, timeout=8)
                res = r.json()
                if res.get("retcode") == 0:
                    self.widget_data = NapWidgetData.from_dict(res.get("data", {}))
                    self.is_online = True
                    self.last_sync_time = time.time()
                    self.after(0, self._update_ui)
                    return
                else:
                    print(f"[!] API 请求返回错误: {res.get('message')}")
            except Exception as e:
                print(f"[!] 网络请求异常: {e}")

        # 若无凭据或请求失败，加载演示 Demo 数据
        demo_dict = {
            "energy": {
                "progress": {"current": 218, "max": 240},
                "restore": 7920 # 约2小时12分
            },
            "vitality": {"current": 400, "max": 400},
            "has_signed": True,
            "note_list": [
                {"name": "刮刮卡", "value": "已刮开", "value_color": "E5FFFFFF"},
                {"name": "录像店营收", "value": "待营业", "value_color": "FFFFC529"},
                {"name": "零号空洞调查点", "value": "8000/8000", "value_color": "E5FFFFFF"},
                {"name": "喝咖啡", "value": "已享用", "value_color": "E5FFFFFF"}
            ]
        }
        self.widget_data = NapWidgetData.from_dict(demo_dict)
        self.is_online = False
        self.after(0, self._update_ui)

    def _update_ui(self):
        """将数据渲染至组件界面"""
        if not self.widget_data:
            return

        d = self.widget_data
        # 1. 电量
        self.energy_val_label.config(text=str(d.energy.progress.current))
        self.energy_total_label.config(text=f"/{d.energy.progress.max}")
        self.restore_label.config(text=d.energy.formatted_restore_time)

        # 电量高亮颜色
        color = EnergyCalculationLogic.get_energy_color_hex(d.energy.progress.current, d.energy.progress.max)
        self.energy_val_label.config(fg=color)

        # 2. 活跃度
        v_status = EnergyCalculationLogic.get_vitality_status(d.vitality.current, d.vitality.max)
        self.vitality_label.config(text=f"今日活跃: {v_status['text']}", fg=v_status['text_color'])

        # 3. 便笺条目
        for idx, (name_lbl, val_lbl) in enumerate(self.note_items_labels):
            if idx < len(d.note_list):
                item = d.note_list[idx]
                name_lbl.config(text=item.name)
                val_lbl.config(text=item.value)
                if item.value_color:
                    # 去掉可能的 alpha 通道前缀
                    c = "#" + item.value_color[-6:]
                    val_lbl.config(fg=c)
            else:
                name_lbl.config(text="")
                val_lbl.config(text="")

        # 4. 底部状态
        now_str = time.strftime("%H:%M:%S")
        if self.is_online:
            self.status_bar.config(text=f"状态: 在线已同步 (最后刷新: {now_str})", fg="#00E676")
        else:
            self.status_bar.config(text="状态: 演示 Demo 模式 (点击【账号登录】绑定真实账号)", fg="#FFC529")


if __name__ == "__main__":
    app = ZzzDesktopWidget()
    app.mainloop()
