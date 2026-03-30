from datetime import datetime

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session

from database import get_db
from models.character import AICharacter
from models.user import User
from schemas.chat import ChatRequest, ChatResponse
from services.llm import call_llm
from services.prompt import build_system_prompt
from services.mood import apply_mood_decay
from services.favorability import update_favor_stage
from services.memory import save_chat_memory, recall_for_prompt
from routers.robot import push_reply
from routers.ws import push_to_robots
from services.currency import reward_chat, check_daily_login

router = APIRouter()


@router.post("/chat", response_model=ChatResponse)
async def chat(req: ChatRequest, db: Session = Depends(get_db)):
    char = db.query(AICharacter).filter(AICharacter.user_id == req.user_id).first()
    if not char:
        raise HTTPException(status_code=404, detail="角色不存在，请先调用 /user/init")

    apply_mood_decay(char)

    # 召回记忆注入 Prompt
    memory_text = recall_for_prompt(db, char.id)
    system_prompt = build_system_prompt(char, memory_text)

    reply = await call_llm(system_prompt, req.message)

    # 保存这轮对话到短期记忆
    save_chat_memory(db, char.id, "用户", req.message)
    save_chat_memory(db, char.id, "小星", reply)

    # 状态微调
    char.favorability += 1
    char.mood = round(max(-1.0, min(1.0, char.mood + 0.02)), 4)
    char.mood_updated_at = datetime.utcnow()
    update_favor_stage(char)

    user = db.query(User).filter(User.id == req.user_id).first()
    if user:
        user.last_active = datetime.utcnow()
        check_daily_login(user)
        reward_chat(user)

    db.commit()
    db.refresh(char)

    push_reply(reply, char.expression, char.favorability, char.mood)

    # WebSocket 实时推送给机器人
    try:
        await push_to_robots(reply, char.expression, char.favorability, round(char.mood, 2))
        print("WebSocket 推送完成")
    except Exception as e:
        print("WebSocket 推送失败:", e)

    return ChatResponse(
        reply=reply,
        mood=round(char.mood, 2),
        favorability=char.favorability,
        favor_stage=char.favor_stage_name,
        expression=char.expression,
    )
