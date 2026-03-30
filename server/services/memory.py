"""
记忆系统服务 — 短期记忆（对话历史）+ 长期记忆（重要事件）+ 摘要 + 召回
"""

from datetime import datetime
from sqlalchemy.orm import Session
from sqlalchemy import desc

from models.memory import Memory
from models.gift import GiftRecord

MAX_SHORT_TERM = 20
MAX_LONG_TERM = 50
MAX_PROMPT_MEMORIES = 8
MAX_MEMORY_CHARS = 800  # 记忆注入 Prompt 的最大字符数，约 400 token


def save_chat_memory(db: Session, character_id: int, role: str, content: str):
    """存储一轮对话到短期记忆"""
    mem = Memory(
        character_id=character_id,
        type="short",
        content=role + ": " + content,
        emotional_weight=0.3,
    )
    db.add(mem)

    # 短期记忆超出上限时删除最旧的
    count = db.query(Memory).filter(
        Memory.character_id == character_id,
        Memory.type == "short"
    ).count()

    if count > MAX_SHORT_TERM:
        oldest = db.query(Memory).filter(
            Memory.character_id == character_id,
            Memory.type == "short"
        ).order_by(Memory.created_at).first()
        if oldest:
            db.delete(oldest)


def save_long_memory(db: Session, character_id: int, content: str, weight: float = 0.7):
    """存储一条长期记忆（重要事件）"""
    # 长期记忆超上限时淘汰权重最低的
    count = db.query(Memory).filter(
        Memory.character_id == character_id,
        Memory.type == "long"
    ).count()

    if count >= MAX_LONG_TERM:
        weakest = db.query(Memory).filter(
            Memory.character_id == character_id,
            Memory.type == "long"
        ).order_by(Memory.emotional_weight).first()
        if weakest:
            db.delete(weakest)

    mem = Memory(
        character_id=character_id,
        type="long",
        content=content,
        emotional_weight=weight,
    )
    db.add(mem)


def save_gift_memory(db: Session, character_id: int, gift_name: str, like_label: str):
    """送礼时自动生成一条长期记忆"""
    now = datetime.utcnow().strftime("%m月%d日")
    content = now + " 用户送了" + gift_name + "，你觉得" + like_label
    save_long_memory(db, character_id, content, weight=0.5)


def recall_for_prompt(db: Session, character_id: int) -> str:
    """召回记忆，拼接为 Prompt 注入文本"""
    parts = []

    # 长期记忆：按权重降序取前 N 条
    long_memories = db.query(Memory).filter(
        Memory.character_id == character_id,
        Memory.type == "long"
    ).order_by(desc(Memory.emotional_weight)).limit(MAX_PROMPT_MEMORIES).all()

    if long_memories:
        parts.append("【长期记忆（重要的事）】")
        for m in long_memories:
            parts.append("- " + m.content)

    # 短期记忆：最近 10 轮对话
    recent = db.query(Memory).filter(
        Memory.character_id == character_id,
        Memory.type == "short"
    ).order_by(desc(Memory.created_at)).limit(10).all()

    if recent:
        parts.append("\n【近期对话记录】")
        for m in reversed(recent):
            parts.append(m.content)

    # 送礼统计
    gift_count = db.query(GiftRecord).filter(
        GiftRecord.character_id == character_id
    ).count()
    if gift_count > 0:
        parts.append("\n【送礼统计】用户已送过 " + str(gift_count) + " 次礼物")

    result = "\n".join(parts) if parts else ""
    if len(result) > MAX_MEMORY_CHARS:
        result = result[:MAX_MEMORY_CHARS] + "\n...（更早的记忆已省略）"
    return result


def get_memory_summary(db: Session, character_id: int) -> dict:
    """给客户端的记忆摘要"""
    short_count = db.query(Memory).filter(
        Memory.character_id == character_id,
        Memory.type == "short"
    ).count()

    long_count = db.query(Memory).filter(
        Memory.character_id == character_id,
        Memory.type == "long"
    ).count()

    long_memories = db.query(Memory).filter(
        Memory.character_id == character_id,
        Memory.type == "long"
    ).order_by(desc(Memory.emotional_weight)).limit(5).all()

    return {
        "short_term_count": short_count,
        "long_term_count": long_count,
        "key_memories": [m.content for m in long_memories],
    }
