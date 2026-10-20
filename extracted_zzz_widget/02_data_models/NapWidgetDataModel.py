# -*- coding: utf-8 -*-
"""
绝区零桌面小组件数据模型 (Data Models)
提取自原版 APK 类：
  - com.mihoyo.hyperion.biz.miniwidget.nap.NapWidgetData
  - com.mihoyo.hyperion.biz.miniwidget.napavatar.AvatarBackground
"""

from dataclasses import dataclass, field
from typing import List, Dict, Optional
import datetime


@dataclass
class EnergyProgress:
    """电量数值进度"""
    current: int = 0      # 当前电量 (例如 215)
    max: int = 240        # 电量上限 (绝区零默认 240)

    @property
    def percentage(self) -> float:
        """获取电量百分比 0.0 ~ 1.0"""
        if self.max <= 0:
            return 0.0
        return min(1.0, max(0.0, self.current / self.max))

    @property
    def is_full(self) -> bool:
        """是否已满电量"""
        return self.current >= self.max


@dataclass
class Energy:
    """电量与恢复倒计时实体"""
    progress: EnergyProgress = field(default_factory=EnergyProgress)
    restore: int = 0      # 完全恢复剩余秒数 (为 0 表示已满)

    @property
    def formatted_restore_time(self) -> str:
        """
        格式化电量恢复提示，与米游社客户端完全一致：
        - 已满时显示: "已全部恢复"
        - 未满时显示恢复所需时间，例如 "2小时14分" 或预测时间 "预计 18:30 恢复"
        """
        if self.progress.is_full or self.restore <= 0:
            return "已全部恢复"
        
        hours = self.restore // 3600
        minutes = (self.restore % 3600) // 60
        
        finish_dt = datetime.datetime.now() + datetime.timedelta(seconds=self.restore)
        time_str = finish_dt.strftime("%H:%M")
        
        if hours > 0:
            return f"预计 {time_str} 恢复 ({hours}小时{minutes}分)"
        else:
            return f"预计 {time_str} 恢复 ({minutes}分钟)"


@dataclass
class Vitality:
    """每日活跃度数据实体"""
    current: int = 0      # 当前活跃度 (0 ~ 400)
    max: int = 400        # 最大活跃度 (400)

    @property
    def is_finished(self) -> bool:
        """今日活跃度是否已领满"""
        return self.current >= self.max


@dataclass
class NoteItem:
    """
    便笺单项条目 (例如：刮刮卡、录像店、咖啡、零号空洞调查点)
    对应 APK 原版 NapWidgetData.b
    """
    name: str = ""        # 便笺名称，如 "零号空洞调查点数"
    name_color: str = ""  # 名称颜色，如 "A6FFFFFF"
    value: str = ""       # 状态数值，如 "8000/8000" 或 "已刮开"
    value_color: str = "" # 数值高亮颜色，如 "E5FFFFFF" 或 "FFFFC529"


@dataclass
class NapWidgetData:
    """
    绝区零桌面小组件完整聚合数据
    对应 APK 原版 NapWidgetData 主实体
    """
    energy: Energy = field(default_factory=Energy)
    vitality: Vitality = field(default_factory=Vitality)
    has_signed: bool = False             # 米游社每日签到状态
    home_url: str = ""                   # 社区主页跳转
    main_home_url: str = ""              # 绝区零板块主页
    note_url: str = ""                   # 战绩详细页面 URL
    sign_url: str = ""                   # 签到页面 URL
    note_list: List[NoteItem] = field(default_factory=list) # 右侧便笺列表
    hoyo_link: Dict[str, str] = field(default_factory=dict) # 游戏/云游戏启动链接

    @classmethod
    def from_dict(cls, data: dict) -> "NapWidgetData":
        """从米游社 API 返回的 JSON 字典构建模型对象"""
        energy_data = data.get("energy", {})
        progress_data = energy_data.get("progress", {})
        energy = Energy(
            progress=EnergyProgress(
                current=progress_data.get("current", 0),
                max=progress_data.get("max", 240)
            ),
            restore=energy_data.get("restore", 0)
        )

        vitality_data = data.get("vitality", {})
        vitality = Vitality(
            current=vitality_data.get("current", 0),
            max=vitality_data.get("max", 400)
        )

        notes = []
        for n in data.get("note_list", []):
            notes.append(NoteItem(
                name=n.get("name", ""),
                name_color=n.get("name_color", ""),
                value=n.get("value", ""),
                value_color=n.get("value_color", "")
            ))

        return cls(
            energy=energy,
            vitality=vitality,
            has_signed=data.get("has_signed", False),
            home_url=data.get("home_url", ""),
            main_home_url=data.get("main_home_url", ""),
            note_url=data.get("note_url", ""),
            sign_url=data.get("sign_url", ""),
            note_list=notes,
            hoyo_link=data.get("hoyo_link_cn", {})
        )


@dataclass
class BackgroundItem:
    """小组件角色背景条目"""
    id: int = 0
    name: str = ""
    bg_url: str = ""
    tab_icon_selected: str = ""
    tab_icon_unselected: str = ""
