from pydantic import BaseModel


class GiftRequest(BaseModel):
    user_id: int
    gift_id: str


class GiftResponse(BaseModel):
    reply: str
    favorability: int
    favor_delta: int
    mood: float
    mood_delta: float
    favor_stage: str
    expression: str


class GiftItem(BaseModel):
    id: str
    name: str
    cost: int
    rarity: str
    category: str = ""


class GiftListResponse(BaseModel):
    gifts: list[GiftItem]
