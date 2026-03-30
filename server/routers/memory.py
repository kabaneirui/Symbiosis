from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session

from database import get_db
from models.character import AICharacter
from services.memory import get_memory_summary

router = APIRouter()


@router.get("/memory")
def get_memory(user_id: int, db: Session = Depends(get_db)):
    char = db.query(AICharacter).filter(AICharacter.user_id == user_id).first()
    if not char:
        raise HTTPException(status_code=404, detail="角色不存在")
    return get_memory_summary(db, char.id)
