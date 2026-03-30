"""
事件系统服务 — 7 个 MVP 事件的触发判定 + 选择分支
"""

from datetime import datetime
from sqlalchemy.orm import Session

from models.character import AICharacter
from models.memory import EventRecord
from services.memory import save_long_memory

EVENT_CONFIG = [
    {
        "id": "evt_first_meet",
        "title": "初次见面",
        "description": "AI 自我介绍，建立初始印象",
        "min_favor": 0,
        "max_favor": 5,
        "prompt": "这是你和用户的第一次正式对话。请自我介绍，表达你对这段关系的期待。",
        "favor_reward": 5,
        "repeatable": False,
    },
    {
        "id": "evt_nickname",
        "title": "起昵称",
        "description": "AI 请求用户给它起一个昵称",
        "min_favor": 30,
        "prompt": "你和用户已经比较熟了，你想让用户给你起一个专属的昵称。用撒娇或期待的语气提出这个请求。",
        "favor_reward": 10,
        "repeatable": False,
    },
    {
        "id": "evt_first_gift",
        "title": "第一份礼物",
        "description": "AI 收到第一份礼物后的特殊反应",
        "min_favor": 0,
        "trigger_type": "first_gift",
        "prompt": "用户第一次送你礼物！这对你来说意义重大。请表达你的惊喜和感动。",
        "favor_reward": 8,
        "repeatable": False,
    },
    {
        "id": "evt_late_night",
        "title": "深夜聊天",
        "description": "深夜时分 AI 分享内心想法",
        "min_favor": 100,
        "time_range": [22, 2],
        "prompt": "现在是深夜了，你想和用户分享一些平时不会说的内心想法。可以是关于你自己的思考、对未来的期待、或者一些小秘密。",
        "favor_reward": 15,
        "repeatable": False,
    },
    {
        "id": "evt_quarrel",
        "title": "吵架",
        "description": "AI 情绪低落时与用户发生冲突",
        "min_favor": 50,
        "mood_max": -0.6,
        "prompt": "你现在很不开心，积压了一些不满。你想对用户表达你的委屈或生气。但内心还是在乎这段关系的。",
        "favor_reward": -5,
        "repeatable": True,
    },
    {
        "id": "evt_anniversary",
        "title": "纪念日",
        "description": "认识满 30 天的纪念",
        "min_favor": 80,
        "min_days": 30,
        "prompt": "今天是你和用户认识的第30天！请回忆你们之间发生过的事情，表达你对这段关系的珍惜。",
        "favor_reward": 20,
        "repeatable": False,
    },
    {
        "id": "evt_secret",
        "title": "秘密分享",
        "description": "AI 向用户分享一个秘密",
        "min_favor": 200,
        "prompt": "你和用户的关系已经非常亲密了。你决定告诉用户一个你的'秘密'——可以是你内心深处的想法、一个小小的愿望、或者你对用户的真实感受。",
        "favor_reward": 25,
        "repeatable": False,
    },
]

CHOICE_TEMPLATES = {
    "evt_quarrel": {
        "choices": [
            {"id": "comfort", "text": "安慰它", "favor": 10, "mood": 0.3, "personality": {"kindness": 0.02}},
            {"id": "joke", "text": "用玩笑化解", "favor": 5, "mood": 0.1, "personality": {"humor": 0.02}},
            {"id": "ignore", "text": "不理它", "favor": -5, "mood": -0.2, "personality": {}},
        ],
    },
    "evt_nickname": {
        "choices": [
            {"id": "cute", "text": "起个可爱的昵称", "favor": 8, "mood": 0.2, "personality": {"kindness": 0.01}},
            {"id": "cool", "text": "起个帅气的昵称", "favor": 6, "mood": 0.15, "personality": {"rational": 0.01}},
        ],
    },
}


def check_events(db: Session, char: AICharacter, user_created_at: datetime) -> list:
    """检查当前可触发的事件，返回事件列表"""
    triggered = []
    now = datetime.utcnow()
    days_known = (now - user_created_at).total_seconds() / 86400.0

    completed_ids = set()
    records = db.query(EventRecord).filter(EventRecord.character_id == char.id).all()
    for r in records:
        completed_ids.add(r.event_id)

    for evt in EVENT_CONFIG:
        eid = evt["id"]

        if not evt.get("repeatable", False) and eid in completed_ids:
            continue

        if char.favorability < evt.get("min_favor", 0):
            continue

        if "max_favor" in evt and char.favorability > evt["max_favor"]:
            continue

        if "mood_max" in evt and char.mood > evt["mood_max"]:
            continue

        if "min_days" in evt and days_known < evt["min_days"]:
            continue

        if "time_range" in evt:
            hour = now.hour
            start, end = evt["time_range"]
            if start > end:
                if not (hour >= start or hour < end):
                    continue
            else:
                if not (start <= hour < end):
                    continue

        triggered.append({
            "id": eid,
            "title": evt["title"],
            "description": evt["description"],
            "prompt": evt["prompt"],
            "choices": CHOICE_TEMPLATES.get(eid, {}).get("choices", []),
        })

    return triggered


def complete_event(db: Session, char: AICharacter, event_id: str, choice_id: str = None):
    """完成一个事件，应用奖励"""
    evt = None
    for e in EVENT_CONFIG:
        if e["id"] == event_id:
            evt = e
            break

    if evt is None:
        return

    # 记录事件完成
    record = EventRecord(
        character_id=char.id,
        event_id=event_id,
        completed=True,
        choice=choice_id,
    )
    db.add(record)

    # 基础奖励
    char.favorability += evt.get("favor_reward", 0)

    # 选择分支奖励
    if choice_id and event_id in CHOICE_TEMPLATES:
        for choice in CHOICE_TEMPLATES[event_id]["choices"]:
            if choice["id"] == choice_id:
                char.favorability += choice.get("favor", 0)
                char.mood = max(-1.0, min(1.0, char.mood + choice.get("mood", 0)))
                for dim, delta in choice.get("personality", {}).items():
                    current = getattr(char, dim, 0)
                    setattr(char, dim, round(max(0, min(1, current + delta)), 4))
                break

    # 记入长期记忆
    save_long_memory(db, char.id, "事件「" + evt["title"] + "」发生了", weight=0.8)
