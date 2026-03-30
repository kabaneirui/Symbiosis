import json
from pathlib import Path

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session

from database import get_db
from models.character import AICharacter
from models.gift import GiftRecord
from schemas.gift import GiftRequest, GiftResponse, GiftItem, GiftListResponse
from services.llm import call_llm
from services.prompt import build_system_prompt, build_gift_context
from services.mood import apply_mood_decay
from services.favorability import update_favor_stage
from services.memory import recall_for_prompt, save_gift_memory
from services.currency import spend_coins, reward_gift, check_daily_login
from models.user import User

router = APIRouter()

GIFT_CONFIG_PATH = Path(__file__).parent.parent / "data" / "gift_config.json"
_gift_config: dict | None = None


def _load_gift_config() -> dict:
    global _gift_config
    if _gift_config is None:
        with open(GIFT_CONFIG_PATH, "r", encoding="utf-8") as f:
            _gift_config = json.load(f)
    return _gift_config


@router.get("/gifts", response_model=GiftListResponse)
def get_gifts():
    config = _load_gift_config()
    items = [
        GiftItem(id=gid, name=g["name"], cost=g["cost"], rarity=g["rarity"], category=g.get("category", ""))
        for gid, g in config.items()
    ]
    return GiftListResponse(gifts=items)


@router.post("/gift", response_model=GiftResponse)
async def send_gift(req: GiftRequest, db: Session = Depends(get_db)):
    config = _load_gift_config()
    gift_data = config.get(req.gift_id)
    if not gift_data:
        raise HTTPException(status_code=400, detail=f"礼物 {req.gift_id} 不存在")

    char = db.query(AICharacter).filter(AICharacter.user_id == req.user_id).first()
    user = db.query(User).filter(User.id == req.user_id).first()
    if not char or not user:
        raise HTTPException(status_code=404, detail="角色不存在")

    # 消耗心意货币
    cost = gift_data.get("cost", 0)
    if cost > 0 and not spend_coins(user, cost):
        raise HTTPException(status_code=400, detail="心意不足，需要 " + str(cost) + "，当前 " + str(user.coins))

    check_daily_login(user)
    apply_mood_decay(char)

    # 计算喜好度：取礼物 tags 中最高的喜好值
    like_score = _calc_like_score(char.preferences or {}, gift_data["tags"])

    # 计算好感增益
    base_favor = gift_data["base_favor"]
    final_favor = int(base_favor * (1.0 + like_score))

    # 查询这个礼物送过多少次
    repeat_count = (
        db.query(GiftRecord)
        .filter(GiftRecord.character_id == char.id, GiftRecord.gift_id == req.gift_id)
        .count()
    )

    if repeat_count == 0:
        # 首次赠送加成 +20%
        final_favor = int(final_favor * 1.2)
    elif repeat_count > 5:
        # 重复赠送衰减：超过5次后每次递减10%，最低50%
        decay = max(0.5, 1.0 - (repeat_count - 5) * 0.1)
        final_favor = int(final_favor * decay)

    final_favor = max(final_favor, 0)

    # 情绪变化
    mood_delta = _calc_mood_delta(like_score)

    # 更新角色状态
    old_favorability = char.favorability
    char.favorability += final_favor
    char.mood = round(max(-1.0, min(1.0, char.mood + mood_delta)), 4)
    update_favor_stage(char)

    # 写入送礼记录
    record = GiftRecord(
        user_id=req.user_id,
        character_id=char.id,
        gift_id=req.gift_id,
        favor_gained=final_favor,
        mood_change=mood_delta,
    )
    db.add(record)

    # 存送礼记忆
    like_label = "超喜欢" if like_score > 0.7 else "挺喜欢" if like_score > 0.3 else "一般" if like_score > -0.3 else "不太喜欢" if like_score > -0.7 else "讨厌"
    save_gift_memory(db, char.id, gift_data["name"], like_label)

    # 生成 AI 回复（带记忆）
    memory_text = recall_for_prompt(db, char.id)
    system_prompt = build_system_prompt(char, memory_text)
    gift_context = build_gift_context(gift_data["name"], like_score)
    user_message = gift_context + "\n\n用户送了你" + gift_data["name"] + "。"
    reply = await call_llm(system_prompt, user_message)

    # 送礼返还心意
    reward_gift(user)

    db.commit()
    db.refresh(char)
    db.refresh(user)

    return GiftResponse(
        reply=reply,
        favorability=char.favorability,
        favor_delta=final_favor,
        mood=round(char.mood, 2),
        mood_delta=round(mood_delta, 2),
        favor_stage=char.favor_stage_name,
        expression=char.expression,
    )


def _calc_like_score(preferences: dict, tags: list[str]) -> float:
    """取礼物 tags 对应喜好值中的最大值"""
    scores = [preferences.get(tag, 0.0) for tag in tags]
    return max(scores) if scores else 0.0


def _calc_mood_delta(like_score: float) -> float:
    if like_score > 0.7:
        return 0.3
    elif like_score > 0.3:
        return 0.15
    elif like_score > -0.3:
        return 0.05
    elif like_score > -0.7:
        return -0.1
    else:
        return -0.25


