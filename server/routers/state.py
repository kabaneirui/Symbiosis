from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session

from database import get_db
from models.character import AICharacter
from schemas.state import StateResponse, PersonalityData

router = APIRouter()


@router.get("/state", response_model=StateResponse)
def get_state(user_id: int, db: Session = Depends(get_db)):
    char = db.query(AICharacter).filter(AICharacter.user_id == user_id).first()
    if not char:
        raise HTTPException(status_code=404, detail="角色不存在，请先调用 /user/init")

    return StateResponse(
        favorability=char.favorability,
        favor_stage=char.favor_stage_name,
        mood=round(char.mood, 2),
        mood_label=char.mood_label,
        personality=PersonalityData(
            kindness=char.kindness,
            tsundere=char.tsundere,
            humor=char.humor,
            rational=char.rational,
        ),
        expression=char.expression,
    )
