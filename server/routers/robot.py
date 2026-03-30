"""
机器人同步接口 — ESP32 轮询获取最新 AI 回复并播放
"""

import base64
from fastapi import APIRouter
from pydantic import BaseModel

from services.voice import text_to_speech

router = APIRouter()

# 缓存最新回复
_latest = {
    "id": 0,
    "reply": "",
    "expression": "expr_calm",
    "favorability": 0,
    "mood": 0.0,
}


def push_reply(reply: str, expression: str, favorability: int, mood: float):
    """被聊天/送礼接口调用，推送最新回复"""
    _latest["id"] += 1
    _latest["reply"] = reply
    _latest["expression"] = expression
    _latest["favorability"] = favorability
    _latest["mood"] = mood


class RobotPollResponse(BaseModel):
    id: int
    reply: str
    expression: str
    favorability: int
    mood: float
    audio_base64: str
    audio_format: str


@router.get("/robot/poll")
async def poll(since_id: int = 0):
    """ESP32 轮询：有新回复就返回文本+TTS音频"""
    if _latest["id"] <= since_id or _latest["reply"] == "":
        return {"id": _latest["id"], "has_new": False}

    # 生成 TTS
    audio = await text_to_speech(_latest["reply"])

    return {
        "id": _latest["id"],
        "has_new": True,
        "reply": _latest["reply"],
        "expression": _latest["expression"],
        "favorability": _latest["favorability"],
        "mood": _latest["mood"],
        "audio_base64": base64.b64encode(audio).decode() if audio else "",
        "audio_format": "mp3",
    }
