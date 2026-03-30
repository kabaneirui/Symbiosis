"""
机器人同步接口 — ESP32 轮询获取最新 AI 回复
"""

import base64
from fastapi import APIRouter
from fastapi.responses import Response

from services.voice import text_to_speech

router = APIRouter()

_latest = {
    "id": 0,
    "reply": "",
    "expression": "expr_calm",
    "favorability": 0,
    "mood": 0.0,
}

_latest_audio = b""


def push_reply(reply: str, expression: str, favorability: int, mood: float):
    _latest["id"] += 1
    _latest["reply"] = reply
    _latest["expression"] = expression
    _latest["favorability"] = favorability
    _latest["mood"] = mood


@router.get("/robot/poll")
async def poll(since_id: int = 0):
    """ESP32 轮询：有新回复返回文本（不含音频，减少传输量）"""
    global _latest_audio

    if _latest["id"] <= since_id or _latest["reply"] == "":
        return {"id": _latest["id"], "has_new": False}

    # 预生成音频缓存（ESP32 单独下载）
    _latest_audio = await text_to_speech(_latest["reply"])

    return {
        "id": _latest["id"],
        "has_new": True,
        "reply": _latest["reply"],
        "expression": _latest["expression"],
        "favorability": _latest["favorability"],
        "mood": _latest["mood"],
        "has_audio": len(_latest_audio) > 0,
    }


@router.get("/robot/audio")
async def get_audio():
    """ESP32 单独下载最新回复的 TTS 音频（mp3 二进制流）"""
    if not _latest_audio:
        return Response(content=b"", media_type="audio/mpeg")
    return Response(content=_latest_audio, media_type="audio/mpeg")
