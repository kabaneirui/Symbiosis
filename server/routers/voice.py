import base64

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from sqlalchemy.orm import Session

from database import get_db
from models.character import AICharacter
from services.voice import text_to_speech, speech_to_text
from services.llm import call_llm
from services.prompt import build_system_prompt
from services.memory import recall_for_prompt, save_chat_memory
from services.favorability import update_favor_stage

router = APIRouter()


class TTSRequest(BaseModel):
    text: str


class TTSResponse(BaseModel):
    audio_base64: str
    format: str


class VoiceChatRequest(BaseModel):
    user_id: int
    audio_base64: str
    audio_format: str = "wav"


class VoiceChatResponse(BaseModel):
    recognized_text: str
    reply: str
    reply_audio_base64: str
    audio_format: str


@router.post("/voice/tts", response_model=TTSResponse)
async def tts(req: TTSRequest):
    audio = await text_to_speech(req.text)
    return TTSResponse(
        audio_base64=base64.b64encode(audio).decode() if audio else "",
        format="mp3",
    )


@router.post("/voice/chat", response_model=VoiceChatResponse)
async def voice_chat(req: VoiceChatRequest, db: Session = Depends(get_db)):
    """语音聊天全链路：语音→文字→AI回复→语音"""
    char = db.query(AICharacter).filter(AICharacter.user_id == req.user_id).first()
    if not char:
        raise HTTPException(status_code=404, detail="角色不存在")

    # STT: 语音转文字
    audio_bytes = base64.b64decode(req.audio_base64)
    user_text = await speech_to_text(audio_bytes, req.audio_format)

    if not user_text:
        return VoiceChatResponse(
            recognized_text="",
            reply="我没听清，你再说一遍？",
            reply_audio_base64="",
            audio_format="mp3",
        )

    # AI 回复
    memory_text = recall_for_prompt(db, char.id)
    system_prompt = build_system_prompt(char, memory_text)
    reply = await call_llm(system_prompt, user_text)

    # 记忆
    save_chat_memory(db, char.id, "用户", user_text)
    save_chat_memory(db, char.id, "小星", reply)

    # 状态更新
    char.favorability += 1
    update_favor_stage(char)
    db.commit()

    # TTS: AI 回复转语音
    reply_audio = await text_to_speech(reply)

    return VoiceChatResponse(
        recognized_text=user_text,
        reply=reply,
        reply_audio_base64=base64.b64encode(reply_audio).decode() if reply_audio else "",
        audio_format="mp3",
    )
