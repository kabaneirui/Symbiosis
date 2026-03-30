"""
流式聊天接口 — 豆包逐句输出 → 分句 TTS → SSE 推送音频
ESP32 和 H5 都可以用
"""

import base64
import json
import asyncio

from fastapi import APIRouter, Depends, HTTPException
from fastapi.responses import StreamingResponse
from pydantic import BaseModel
from sqlalchemy.orm import Session

from database import get_db
from models.character import AICharacter
from models.user import User
from services.llm import call_llm_stream
from services.prompt import build_system_prompt
from services.memory import recall_for_prompt, save_chat_memory
from services.mood import apply_mood_decay
from services.favorability import update_favor_stage
from services.currency import reward_chat, check_daily_login
from services.voice import text_to_speech
from routers.robot import push_reply

router = APIRouter()

SENTENCE_ENDS = set("。！？～…\n.!?")


class StreamChatRequest(BaseModel):
    user_id: int
    message: str


@router.post("/chat/stream")
async def stream_chat(req: StreamChatRequest, db: Session = Depends(get_db)):
    """流式聊天：豆包逐句 → TTS → SSE 推送"""
    char = db.query(AICharacter).filter(AICharacter.user_id == req.user_id).first()
    user = db.query(User).filter(User.id == req.user_id).first()
    if not char or not user:
        raise HTTPException(status_code=404, detail="角色不存在")

    apply_mood_decay(char)
    memory_text = recall_for_prompt(db, char.id)
    system_prompt = build_system_prompt(char, memory_text)

    async def generate():
        full_reply = ""
        buffer = ""
        sentence_idx = 0

        async for chunk in call_llm_stream(system_prompt, req.message):
            full_reply += chunk
            buffer += chunk

            # 按句子切分
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

                if len(sentence) < 2:
                    continue

                # 发送文本
                yield "data: " + json.dumps({
                    "type": "text",
                    "index": sentence_idx,
                    "text": sentence,
                }, ensure_ascii=False) + "\n\n"

                # 生成 TTS 并发送音频
                audio = await text_to_speech(sentence)
                if audio:
                    yield "data: " + json.dumps({
                        "type": "audio",
                        "index": sentence_idx,
                        "audio_base64": base64.b64encode(audio).decode(),
                    }) + "\n\n"

                sentence_idx += 1

        # 剩余文本
        if buffer.strip():
            sentence = buffer.strip()
            yield "data: " + json.dumps({
                "type": "text",
                "index": sentence_idx,
                "text": sentence,
            }, ensure_ascii=False) + "\n\n"

            audio = await text_to_speech(sentence)
            if audio:
                yield "data: " + json.dumps({
                    "type": "audio",
                    "index": sentence_idx,
                    "audio_base64": base64.b64encode(audio).decode(),
                }) + "\n\n"

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
        db.refresh(char)

        push_reply(full_reply, char.expression, char.favorability, char.mood)

        # 完成信号
        yield "data: " + json.dumps({
            "type": "done",
            "full_reply": full_reply,
            "favorability": char.favorability,
            "mood": round(char.mood, 2),
            "expression": char.expression,
            "favor_stage": char.favor_stage_name,
        }, ensure_ascii=False) + "\n\n"

    return StreamingResponse(generate(), media_type="text/event-stream")
