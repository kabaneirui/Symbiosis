from datetime import datetime

from sqlalchemy import Integer, Float, String, DateTime, ForeignKey, JSON
from sqlalchemy.orm import Mapped, mapped_column, relationship

from database import Base


class AICharacter(Base):
    __tablename__ = "ai_characters"

    id: Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    user_id: Mapped[int] = mapped_column(ForeignKey("users.id"), unique=True)
    name: Mapped[str] = mapped_column(String(50), default="小星")

    # 性格四维
    kindness: Mapped[float] = mapped_column(Float, default=0.5)
    tsundere: Mapped[float] = mapped_column(Float, default=0.3)
    humor: Mapped[float] = mapped_column(Float, default=0.4)
    rational: Mapped[float] = mapped_column(Float, default=0.4)

    # 情绪
    mood: Mapped[float] = mapped_column(Float, default=0.0)
    mood_updated_at: Mapped[datetime] = mapped_column(DateTime, default=datetime.utcnow)

    # 好感度
    favorability: Mapped[int] = mapped_column(Integer, default=0)
    favor_stage: Mapped[int] = mapped_column(Integer, default=0)  # 0陌生 1熟悉 2依赖 3亲密

    # 喜好（JSON 存储 {"flower": 0.9, "coffee": 0.6, ...}）
    preferences: Mapped[dict] = mapped_column(JSON, default=dict)

    created_at: Mapped[datetime] = mapped_column(DateTime, default=datetime.utcnow)

    user: Mapped["User"] = relationship(back_populates="character")

    @property
    def favor_stage_name(self) -> str:
        return ["stranger", "familiar", "dependent", "intimate"][self.favor_stage]

    @property
    def mood_label(self) -> str:
        if self.mood > 0.7:
            return "兴奋"
        elif self.mood > 0.3:
            return "开心"
        elif self.mood > -0.3:
            return "平静"
        elif self.mood > -0.7:
            return "低落"
        else:
            return "难过"

    @property
    def expression(self) -> str:
        if self.mood > 0.7:
            return "expr_excited"
        elif self.mood > 0.3:
            return "expr_happy"
        elif self.mood > -0.3:
            return "expr_calm"
        elif self.mood > -0.7:
            return "expr_sad"
        else:
            return "expr_angry"
