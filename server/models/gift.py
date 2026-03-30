from datetime import datetime

from sqlalchemy import Integer, Float, String, DateTime, ForeignKey
from sqlalchemy.orm import Mapped, mapped_column

from database import Base


class GiftRecord(Base):
    __tablename__ = "gift_records"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    user_id: Mapped[int] = mapped_column(ForeignKey("users.id"))
    character_id: Mapped[int] = mapped_column(ForeignKey("ai_characters.id"))
    gift_id: Mapped[str] = mapped_column(String(50))
    favor_gained: Mapped[int] = mapped_column(Integer, default=0)
    mood_change: Mapped[float] = mapped_column(Float, default=0.0)
    created_at: Mapped[datetime] = mapped_column(DateTime, default=datetime.utcnow)
