"""
性格系统服务 — 行为→性格变化映射 + 学习率衰减 + 每日上限
"""

from datetime import datetime, date
from sqlalchemy.orm import Session

from models.character import AICharacter

BEHAVIOR_MAP = {
    "comfort":    {"kindness": +0.020},
    "complain":   {"tsundere": +0.015},
    "joke":       {"humor":    +0.020},
    "serious":    {"rational": +0.015},
    "ignore":     {"kindness": -0.010},
    "praise":     {"tsundere": -0.010},
}

DAILY_LIMIT = 0.05

_daily_changes: dict[int, dict[str, dict[str, float]]] = {}


def apply_behavior(char: AICharacter, behavior: str):
    """根据用户行为更新性格数值"""
    deltas = BEHAVIOR_MAP.get(behavior)
    if not deltas:
        return

    today = date.today().isoformat()
    key = char.id
    if key not in _daily_changes or today not in _daily_changes.get(key, {}):
        _daily_changes[key] = {today: {"kindness": 0, "tsundere": 0, "humor": 0, "rational": 0}}

    today_changes = _daily_changes[key][today]

    for dim, raw_delta in deltas.items():
        current = getattr(char, dim)
        adjusted_delta = _apply_decay(current, raw_delta)

        if abs(today_changes[dim] + adjusted_delta) > DAILY_LIMIT:
            remaining = DAILY_LIMIT - abs(today_changes[dim])
            if remaining <= 0:
                continue
            adjusted_delta = remaining if raw_delta > 0 else -remaining

        new_val = max(0.0, min(1.0, current + adjusted_delta))
        setattr(char, dim, round(new_val, 4))
        today_changes[dim] += adjusted_delta


def apply_personality_regression(char: AICharacter, inactive_days: float):
    """性格回归张力 — 长期不互动时性格缓慢回归初始值"""
    if inactive_days < 1:
        return

    DEFAULTS = {"kindness": 0.5, "tsundere": 0.3, "humor": 0.4, "rational": 0.4}
    REGRESSION_RATE = 0.005

    for dim, default_val in DEFAULTS.items():
        current = getattr(char, dim)
        diff = default_val - current
        if abs(diff) < 0.001:
            continue
        change = diff * REGRESSION_RATE * min(inactive_days, 7)
        setattr(char, dim, round(current + change, 4))


def _apply_decay(current: float, delta: float) -> float:
    """学习率衰减：越接近极值，变化量越小"""
    if delta > 0:
        decay = 1.0 - current
    else:
        decay = current
    return delta * decay
