"""
一体化语音聊天接口 — 输入文字，直接返回 MP3 音频流
内部：豆包逐句生成 → 分句 TTS → 拼接成完整 MP3 返回
ESP32 直接下载播放，不需要解析 JSON/base64
"""

import asyncio
import io
from fastapi import APIRouter, Depends, HTTPException
from fastapi.responses import StreamingResponse
from pydantic import BaseModel
from sqlalchemy.orm import Session

from database import get_db
from models.character import AICharacter
from models.user import User
from services.llm import call_llm, call_llm_stream
from services.prompt import build_system_prompt
from services.memory import recall_for_prompt, save_chat_memory
from services.mood import apply_mood_decay
from services.favorability import update_favor_stage
from services.currency import reward_chat, check_daily_login
from services.voice import text_to_speech
from routers.robot import push_reply

router = APIRouter()

SENTENCE_ENDS = set("。！？～…\n.!?")


class SpeakRequest(BaseModel):
    user_id: int
    message: str


@router.post("/speak")
async def speak(req: SpeakRequest, db: Session = Depends(get_db)):
    """文字输入 → 返回 AI 回复的 MP3 音频（直接可播放）"""
    char = db.query(AICharacter).filter(AICharacter.user_id == req.user_id).first()
    user = db.query(User).filter(User.id == req.user_id).first()
    if not char or not user:
        raise HTTPException(status_code=404, detail="角色不存在")

    apply_mood_decay(char)
    memory_text = recall_for_prompt(db, char.id)
    system_prompt = build_system_prompt(char, memory_text)

    # 流式收集 LLM 回复，按句子切分并行 TTS
    full_reply = ""
    sentences = []
    buffer = ""

    async for chunk in call_llm_stream(system_prompt, req.message):
        full_reply += chunk
        buffer += chunk

        while True:
            split_pos = -1
            for i, c in enumerate(buffer):
                if c in SENTENCE_ENDS:
                    split_pos = i
                    break
            if split_pos < 0:
                break
            sentence = buffer[:split_pos + 1].strip()
            buffer = buffer[split_pos + 1:]
            if len(sentence) >= 2:
                sentences.append(sentence)

    if buffer.strip():
        sentences.append(buffer.strip())

    # 并发生成所有句子的 TTS
    audio_tasks = [text_to_speech(s) for s in sentences]
    audio_results = await asyncio.gather(*audio_tasks)

    # 拼接成完整 MP3
    combined = io.BytesIO()
    for audio in audio_results:
        if audio:
            combined.write(audio)

    # 更新状态
    save_chat_memory(db, char.id, "用户", req.message)
    save_chat_memory(db, char.id, "小星", full_reply)
    char.favorability += 1
    char.mood = round(max(-1.0, min(1.0, char.mood + 0.02)), 4)
    update_favor_stage(char)
    from datetime import datetime
    user.last_active = datetime.utcnow()
    check_daily_login(user)
    reward_chat(user)
    db.commit()

    push_reply(full_reply, char.expression, char.favorability, char.mood)

    # 把文字回复放在 HTTP header 里，音频放 body
    headers = {
        "X-Reply": full_reply[:200].encode("utf-8").decode("latin-1", errors="replace"),
        "X-Favorability": str(char.favorability),
        "X-Expression": char.expression,
        "Content-Type": "audio/mpeg",
    }

    combined.seek(0)
    return StreamingResponse(combined, media_type="audio/mpeg", headers=headers)
