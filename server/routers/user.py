from datetime import datetime

from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session

from database import get_db
from models.user import User
from models.character import AICharacter
from schemas.state import UserInitRequest, UserInitResponse

router = APIRouter()

DEFAULT_PREFERENCES = {
    "flower": 0.9,
    "coffee": 0.6,
    "book": -0.3,
    "spider_toy": -0.8,
    "music": 0.7,
    "cooking": 0.4,
    "special": 0.5,
    "romantic": 0.6,
    "nature": 0.7,
    "drink": 0.3,
    "knowledge": 0.1,
    "gift": 0.4,
    "food": 0.5,
    "sweet": 0.7,
    "cute": 0.85,
    "tech": 0.3,
    "warm": 0.6,
    "clothing": 0.4,
    "luxury": 0.5,
    "gaming": 0.2,
    "prank": -0.7,
    "personal": 0.9,
}


@router.post("/user/init", response_model=UserInitResponse)
def init_user(req: UserInitRequest, db: Session = Depends(get_db)):
    # 先查找已有用户（按昵称匹配）
    existing = db.query(User).filter(User.nickname == req.nickname).first()

    if existing:
        existing.last_active = datetime.utcnow()
        char = db.query(AICharacter).filter(AICharacter.user_id == existing.id).first()
        db.commit()

        return UserInitResponse(
            user_id=existing.id,
            character_name=char.name if char else "小星",
            message="欢迎回来！" + (char.name if char else "小星") + "一直在等你呢～",
        )

    # 新用户：创建用户 + AI 角色
    user = User(nickname=req.nickname)
    db.add(user)
    db.flush()

    character = AICharacter(
        user_id=user.id,
        name="小星",
        kindness=0.5,
        tsundere=0.3,
        humor=0.4,
        rational=0.4,
        mood=0.1,
        favorability=0,
        favor_stage=0,
        preferences=DEFAULT_PREFERENCES,
    )
    db.add(character)
    db.commit()
    db.refresh(user)
    db.refresh(character)

    return UserInitResponse(
        user_id=user.id,
        character_name=character.name,
        message="你好！我是" + character.name + "，很高兴认识你～",
    )
