from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from sqlalchemy.orm import Session

from database import get_db
from models.character import AICharacter
from models.user import User
from services.events import check_events, complete_event
from services.llm import call_llm
from services.prompt import build_system_prompt
from services.memory import recall_for_prompt

router = APIRouter()


class EventCompleteRequest(BaseModel):
    user_id: int
    event_id: str
    choice_id: str = None


@router.get("/events")
def get_events(user_id: int, db: Session = Depends(get_db)):
    char = db.query(AICharacter).filter(AICharacter.user_id == user_id).first()
    user = db.query(User).filter(User.id == user_id).first()
    if not char or not user:
        raise HTTPException(status_code=404, detail="角色不存在")

    events = check_events(db, char, user.created_at)
    return {"events": events}


@router.post("/events/complete")
async def complete(req: EventCompleteRequest, db: Session = Depends(get_db)):
    char = db.query(AICharacter).filter(AICharacter.user_id == req.user_id).first()
    if not char:
        raise HTTPException(status_code=404, detail="角色不存在")

    # 找到事件的 prompt
    events = check_events(db, char, db.query(User).get(req.user_id).created_at)
    event_prompt = ""
    for e in events:
        if e["id"] == req.event_id:
            event_prompt = e["prompt"]
            break

    if not event_prompt:
        raise HTTPException(status_code=400, detail="该事件当前不可触发")

    # 生成事件对话
    memory_text = recall_for_prompt(db, char.id)
    system_prompt = build_system_prompt(char, memory_text)
    reply = await call_llm(system_prompt, "【事件触发】" + event_prompt)

    complete_event(db, char, req.event_id, req.choice_id)

    db.commit()
    db.refresh(char)

    return {
        "reply": reply,
        "event_id": req.event_id,
        "favorability": char.favorability,
        "mood": round(char.mood, 2),
        "favor_stage": char.favor_stage_name,
    }
