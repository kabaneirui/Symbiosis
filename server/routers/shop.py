from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from sqlalchemy.orm import Session

from database import get_db
from models.user import User
from models.character import AICharacter
from services.shop import get_shop, open_mystery_box, _load_gifts
from services.currency import spend_coins, check_daily_login, get_tasks_status, claim_task_reward

router = APIRouter()


@router.get("/shop")
def shop(user_id: int, db: Session = Depends(get_db)):
    """获取商店数据（推荐 + 每日精选 + 分类）"""
    user = db.query(User).filter(User.id == user_id).first()
    char = db.query(AICharacter).filter(AICharacter.user_id == user_id).first()
    if not user or not char:
        raise HTTPException(status_code=404, detail="用户不存在")

    # 检查每日登录奖励
    login_earned = check_daily_login(user)
    db.commit()

    shop_data = get_shop(char.preferences or {})
    shop_data["coins"] = user.coins
    shop_data["login_streak"] = user.login_streak
    if login_earned > 0:
        shop_data["login_reward"] = login_earned
        shop_data["login_message"] = "每日登录奖励 +" + str(login_earned) + " 心意！"
        if user.login_streak > 1:
            shop_data["login_message"] += "（连续" + str(user.login_streak) + "天）"

    return shop_data


class MysteryBoxRequest(BaseModel):
    user_id: int


@router.post("/shop/mystery")
def mystery_box(req: MysteryBoxRequest, db: Session = Depends(get_db)):
    """开启神秘盒子（消耗30心意）"""
    user = db.query(User).filter(User.id == req.user_id).first()
    if not user:
        raise HTTPException(status_code=404, detail="用户不存在")

    cost = 30
    if not spend_coins(user, cost):
        raise HTTPException(status_code=400, detail="心意不足")

    gifts = _load_gifts()
    result = open_mystery_box(gifts)
    result["coins_remaining"] = user.coins

    db.commit()
    return result


@router.get("/tasks")
def tasks(user_id: int, db: Session = Depends(get_db)):
    """获取任务列表和完成状态"""
    user = db.query(User).filter(User.id == user_id).first()
    char = db.query(AICharacter).filter(AICharacter.user_id == user_id).first()
    if not user or not char:
        raise HTTPException(status_code=404, detail="用户不存在")

    status = get_tasks_status(user, char.favorability)
    status["coins"] = user.coins
    return status


class ClaimRequest(BaseModel):
    user_id: int
    task_id: str


@router.post("/tasks/claim")
def claim(req: ClaimRequest, db: Session = Depends(get_db)):
    """领取任务奖励"""
    user = db.query(User).filter(User.id == req.user_id).first()
    char = db.query(AICharacter).filter(AICharacter.user_id == req.user_id).first()
    if not user or not char:
        raise HTTPException(status_code=404, detail="用户不存在")

    reward = claim_task_reward(user, req.task_id, char.favorability)
    db.commit()

    return {
        "task_id": req.task_id,
        "reward": reward,
        "coins": user.coins,
        "message": "获得 " + str(reward) + " 心意！" if reward > 0 else "任务未完成或已领取",
    }
