from pydantic import BaseModel


class PersonalityData(BaseModel):
    kindness: float
    tsundere: float
    humor: float
    rational: float


class StateResponse(BaseModel):
    favorability: int
    favor_stage: str
    mood: float
    mood_label: str
    personality: PersonalityData
    expression: str


class UserInitRequest(BaseModel):
    nickname: str = "用户"


class UserInitResponse(BaseModel):
    user_id: int
    character_name: str
    message: str
