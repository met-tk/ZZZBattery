# -*- coding: utf-8 -*-
"""
绝区零小组件电量计算、状态映射与业务规则
提取自米游社 Android 原版 NapWidgetWorker.java 中的 UI 映射与状态处理逻辑。
"""

import time
from typing import Dict, Any, Tuple


class EnergyCalculationLogic:
    """
    电量与状态计算引擎
    对应 NapWidgetWorker.java:
      - a0: 渲染主小组件
      - Q: 渲染活跃度
      - R: 渲染便笺条目
    """

    # 绝区零电量恢复速率：每 6 分钟恢复 1 点体力 (360 秒 / 点)
    ENERGY_RECOVERY_SECONDS_PER_POINT = 360

    @staticmethod
    def calculate_current_energy(initial_current: int, 
                                 max_energy: int, 
                                 initial_restore_seconds: int, 
                                 elapsed_seconds: int) -> Tuple[int, int]:
        """
        本地时间推算电量与剩余秒数（避免频繁请求米游社 API）
        返回: (当前电量, 剩余完全恢复秒数)
        """
        if initial_current >= max_energy or initial_restore_seconds <= 0:
            return max_energy, 0

        # 如果时间已经超过完全恢复时间
        if elapsed_seconds >= initial_restore_seconds:
            return max_energy, 0

        new_restore = max(0, initial_restore_seconds - elapsed_seconds)
        # 恢复的点数 = (恢复的总秒数 - 剩余秒数) // 360
        total_needed_seconds = (max_energy - initial_current) * EnergyCalculationLogic.ENERGY_RECOVERY_SECONDS_PER_POINT
        restored_seconds = total_needed_seconds - new_restore
        gained_energy = restored_seconds // EnergyCalculationLogic.ENERGY_RECOVERY_SECONDS_PER_POINT
        new_current = min(max_energy, initial_current + gained_energy)

        return new_current, new_restore

    @staticmethod
    def get_energy_color_hex(current: int, max_val: int = 240) -> str:
        """
        获取电量文本与环形/横条进度的高亮颜色
        - 满电 (>= 240): #00E676 (绿色满电提醒)
        - 充足 (>= 160): #FFFFFF (正常白光)
        - 告急 (< 40): #FF5252 (电量耗尽红光)
        - 常用状态: #FFFFFF
        """
        if current >= max_val:
            return "#00E676"
        elif current <= 40:
            return "#FF5252"
        return "#FFFFFF"

    @staticmethod
    def get_vitality_status(current: int, max_val: int = 400) -> Dict[str, Any]:
        """
        获取今日每日活跃度展示状态
        对应 NapWidgetWorker.Q 方法
        - current < max: 显示浅灰色，提示未做完
        - current >= max: 显示白色/高亮色，提示已完成
        """
        is_completed = current >= max_val
        return {
            "text": f"{current}/{max_val}",
            "is_completed": is_completed,
            "text_color": "#FFFFFF" if is_completed else "#9E9E9E",
            "status_label": "今日已达成" if is_completed else "进行中"
        }

    @staticmethod
    def format_sign_in_status(has_signed: bool) -> Dict[str, str]:
        """
        签到状态展示映射
        对应 NapWidgetWorker.a0:
          - has_signed == True: "前往社区" (已完成签到)
          - has_signed == False: "尚未签到" (待签到)
        """
        if has_signed:
            return {
                "text": "已签到",
                "action_text": "前往社区",
                "color": "#9E9E9E"
            }
        else:
            return {
                "text": "尚未签到",
                "action_text": "点击签到",
                "color": "#FFC107" # 醒目黄色提倡签到
            }


if __name__ == "__main__":
    calc = EnergyCalculationLogic()
    print("电量推算测试:")
    now_e, now_r = calc.calculate_current_energy(200, 240, 14400, 720) # 过去12分钟(720秒)恢复2点
    print(f"原电量 200, 恢复后: {now_e}, 剩余秒数: {now_r}")
