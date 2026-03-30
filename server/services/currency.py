"""
货币系统 — "心意" 货币的获取与消耗
"""

from datetime import datetime, date
from sqlalchemy.orm import Session

from models.user import User

# 货币获取规则
REWARDS = {
    "chat": 5,           # 每次聊天
    "daily_login": 50,   # 每日首次登录
    "gift_sent": 10,     # 每次送礼额外返还
    "streak_3": 30,      # 连续3天登录
    "streak_7": 80,      # 连续7天登录
    "streak_30": 300,    # 连续30天登录
}

# 每日任务
DAILY_TASKS = [
    {"id": "chat_3", "name": "聊天达人", "desc": "今日聊天3次", "target": 3, "type": "chat", "reward": 20},
    {"id": "gift_1", "name": "心意满满", "desc": "今日送礼1次", "target": 1, "type": "gift", "reward": 15},
]

# 成长任务
GROWTH_TASKS = [
    {"id": "favor_50", "name": "初识伙伴", "desc": "好感度达到50", "target": 50, "type": "favor", "reward": 100},
    {"id": "favor_100", "name": "知心好友", "desc": "好感度达到100", "target": 100, "type": "favor", "reward": 200},
    {"id": "favor_200", "name": "灵魂伴侣", "desc": "好感度达到200", "target": 200, "type": "favor", "reward": 500},
    {"id": "streak_3", "name": "三日之约", "desc": "连续登录3天", "target": 3, "type": "streak", "reward": 50},
    {"id": "streak_7", "name": "一周陪伴", "desc": "连续登录7天", "target": 7, "type": "streak", "reward": 150},
]

# 已完成的成长任务缓存（简单用内存存，重启清空，后续可持久化）
_completed_growth: dict[int, set] = {}


def check_daily_login(user: User) -> int:
    """检查每日登录，返回获得的心意"""
    today = date.today().isoformat()
    earned = 0

    if user.last_login_date != today:
        # 新的一天
        if user.last_login_date == (date.today().replace(day=date.today().day)).isoformat():
            pass  # same day, skip

        # 计算连续登录
        from datetime import timedelta
        yesterday = (date.today() - timedelta(days=1)).isoformat()
        if user.last_login_date == yesterday:
            user.login_streak += 1
        else:
            user.login_streak = 1

        user.last_login_date = today
        user.chats_today = 0
        user.gifts_today = 0

        # 每日登录奖励
        earned += REWARDS["daily_login"]

        # 连续登录奖励
        if user.login_streak == 3:
            earned += REWARDS["streak_3"]
        elif user.login_streak == 7:
            earned += REWARDS["streak_7"]
        elif user.login_streak == 30:
            earned += REWARDS["streak_30"]

        user.coins += earned

    return earned


def reward_chat(user: User) -> int:
    """聊天获得心意"""
    earned = REWARDS["chat"]
    user.coins += earned
    user.chats_today += 1
    return earned


def reward_gift(user: User) -> int:
    """送礼额外返还心意"""
    earned = REWARDS["gift_sent"]
    user.coins += earned
    user.gifts_today += 1
    return earned


def spend_coins(user: User, amount: int) -> bool:
    """消耗心意，余额不足返回 False"""
    if user.coins < amount:
        return False
    user.coins -= amount
    return True


def get_tasks_status(user: User, favorability: int) -> dict:
    """获取任务完成状态"""
    uid = user.id
    if uid not in _completed_growth:
        _completed_growth[uid] = set()

    daily = []
    for t in DAILY_TASKS:
        current = user.chats_today if t["type"] == "chat" else user.gifts_today
        daily.append({
            "id": t["id"],
            "name": t["name"],
            "desc": t["desc"],
            "progress": min(current, t["target"]),
            "target": t["target"],
            "reward": t["reward"],
            "completed": current >= t["target"],
        })

    growth = []
    for t in GROWTH_TASKS:
        if t["type"] == "favor":
            current = favorability
        elif t["type"] == "streak":
            current = user.login_streak
        else:
            current = 0

        completed = t["id"] in _completed_growth[uid]
        claimable = current >= t["target"] and not completed

        growth.append({
            "id": t["id"],
            "name": t["name"],
            "desc": t["desc"],
            "progress": min(current, t["target"]),
            "target": t["target"],
            "reward": t["reward"],
            "completed": completed,
            "claimable": claimable,
        })

    return {"daily": daily, "growth": growth}


def claim_task_reward(user: User, task_id: str, favorability: int) -> int:
    """领取任务奖励，返回获得的心意"""
    uid = user.id
    if uid not in _completed_growth:
        _completed_growth[uid] = set()

    # 检查每日任务
    for t in DAILY_TASKS:
        if t["id"] == task_id:
            current = user.chats_today if t["type"] == "chat" else user.gifts_today
            if current >= t["target"]:
                user.coins += t["reward"]
                return t["reward"]
            return 0

    # 检查成长任务
    for t in GROWTH_TASKS:
        if t["id"] == task_id:
            if task_id in _completed_growth[uid]:
                return 0  # 已领取
            if t["type"] == "favor":
                current = favorability
            elif t["type"] == "streak":
                current = user.login_streak
            else:
                current = 0
            if current >= t["target"]:
                _completed_growth[uid].add(task_id)
                user.coins += t["reward"]
                return t["reward"]
            return 0

    return 0
