"""
好感度系统服务 — 衰减 + 阶段跃迁
"""

from datetime import datetime

from models.character import AICharacter
from models.user import User

STAGE_THRESHOLDS = [0, 50, 100, 200]
STAGE_FLOORS = [0, 50, 100, 200]


def apply_favor_decay(char: AICharacter, user: User):
    """好感度衰减 — 根据不互动天数递减，不低于当前阶段下限"""
    now = datetime.utcnow()
    inactive_days = (now - user.last_active).total_seconds() / 86400.0

    if inactive_days < 3:
        return

    if inactive_days >= 7:
        decay = int((inactive_days - 6) * 5 + (7 - 3) * 2)
    else:
        decay = int((inactive_days - 2) * 2)

    floor = STAGE_FLOORS[char.favor_stage]
    char.favorability = max(floor, char.favorability - decay)
    update_favor_stage(char)


def update_favor_stage(char: AICharacter):
    """根据好感度更新阶段"""
    if char.favorability >= 200:
        char.favor_stage = 3
    elif char.favorability >= 100:
        char.favor_stage = 2
    elif char.favorability >= 50:
        char.favor_stage = 1
    else:
        char.favor_stage = 0
