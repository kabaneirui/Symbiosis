from pydantic import BaseModel


class ChatRequest(BaseModel):
    user_id: int
    message: str


class ChatResponse(BaseModel):
    reply: str
    mood: float
    favorability: int
    favor_stage: str
    expression: str
