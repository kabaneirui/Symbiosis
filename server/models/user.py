from datetime import datetime

from sqlalchemy import String, Integer, DateTime
from sqlalchemy.orm import Mapped, mapped_column, relationship

from database import Base


class User(Base):
    __tablename__ = "users"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    nickname: Mapped[str] = mapped_column(String(50), default="用户")
    created_at: Mapped[datetime] = mapped_column(DateTime, default=datetime.utcnow)
    last_active: Mapped[datetime] = mapped_column(DateTime, default=datetime.utcnow)
    subscription: Mapped[int] = mapped_column(Integer, default=0)
    coins: Mapped[int] = mapped_column(Integer, default=100)  # 心意货币，初始100
    login_streak: Mapped[int] = mapped_column(Integer, default=0)  # 连续登录天数
    last_login_date: Mapped[str] = mapped_column(String(10), default="")  # 上次登录日期 YYYY-MM-DD
    gifts_today: Mapped[int] = mapped_column(Integer, default=0)  # 今日送礼次数
    chats_today: Mapped[int] = mapped_column(Integer, default=0)  # 今日聊天次数

    character: Mapped["AICharacter"] = relationship(back_populates="user", uselist=False)
