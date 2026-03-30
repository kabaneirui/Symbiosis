"""
情绪系统服务 — 自然衰减 + 触发变化
"""

from datetime import datetime

from models.character import AICharacter

DECAY_RATE = 0.02  # 每小时衰减率


def apply_mood_decay(char: AICharacter):
    """情绪自然衰减 — 随时间回归 0（平静）"""
    now = datetime.utcnow()
    delta_hours = (now - char.mood_updated_at).total_seconds() / 3600.0

    if delta_hours < 0.01:
        return

    char.mood = char.mood * (1.0 - DECAY_RATE * delta_hours)

    if abs(char.mood) < 0.01:
        char.mood = 0.0

    char.mood = round(max(-1.0, min(1.0, char.mood)), 4)
    char.mood_updated_at = now
