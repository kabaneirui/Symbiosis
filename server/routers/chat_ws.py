"""
WebSocket 流式聊天 — LLM 逐句输出 → 分句 TTS → 实时推送给 ESP32
"""

import asyncio
import base64
import json

from fastapi import WebSocket
from sqlalchemy.orm import Session

from database import SessionLocal
from models.character import AICharacter
from models.user import User
from services.llm import call_llm_stream
from services.prompt import build_system_prompt
from services.memory import recall_for_prompt, save_chat_memory
from services.mood import apply_mood_decay
from services.favorability import update_favor_stage
from services.voice import text_to_speech

SENTENCE_DELIMITERS = {"。", "！", "？", "～", "~", "…", "\n", ".", "!", "?"}


async def handle_ws_chat(ws: WebSocket, msg: dict):
    """处理来自 WebSocket 的聊天请求，流式返回"""
    user_id = msg.get("user_id", 0)
    message = msg.get("message", "")

    if not message:
        return

    db = SessionLocal()
    try:
        char = db.query(AICharacter).filter(AICharacter.user_id == user_id).first()
        if not char:
            await ws.send_text(json.dumps({"type": "error", "message": "角色不存在"}))
            return

        apply_mood_decay(char)
        memory_text = recall_for_prompt(db, char.id)
        system_prompt = build_system_prompt(char, memory_text)

        # 流式接收 LLM 回复，按句子切分
        full_reply = ""
        sentence_buffer = ""
        sentence_index = 0

        async for chunk in call_llm_stream(system_prompt, message):
            full_reply += chunk
            sentence_buffer += chunk

            # 检查是否有完整句子
            while True:
                split_pos = -1
                for i, c in enumerate(sentence_buffer):
                    if c in SENTENCE_DELIMITERS:
                        split_pos = i
                        break

                if split_pos < 0:
                    break

                sentence = sentence_buffer[:split_pos + 1].strip()
                sentence_buffer = sentence_buffer[split_pos + 1:]

                if len(sentence) < 2:
                    continue

                # 发送文本片段
                await ws.send_text(json.dumps({
                    "type": "reply_chunk",
                    "index": sentence_index,
                    "text": sentence,
                    "full_text": full_reply,
                }, ensure_ascii=False))

                # 异步生成 TTS 并发送
                audio = await text_to_speech(sentence)
                if audio:
                    await ws.send_text(json.dumps({
                        "type": "audio_chunk",
                        "index": sentence_index,
                        "audio_base64": base64.b64encode(audio).decode(),
                        "audio_format": "mp3",
                    }))

                sentence_index += 1

        # 发送剩余的文本
        if sentence_buffer.strip():
            await ws.send_text(json.dumps({
                "type": "reply_chunk",
                "index": sentence_index,
                "text": sentence_buffer.strip(),
                "full_text": full_reply,
            }, ensure_ascii=False))

            audio = await text_to_speech(sentence_buffer.strip())
            if audio:
                await ws.send_text(json.dumps({
                    "type": "audio_chunk",
                    "index": sentence_index,
                    "audio_base64": base64.b64encode(audio).decode(),
                    "audio_format": "mp3",
                }))

        # 更新状态
        save_chat_memory(db, char.id, "用户", message)
        save_chat_memory(db, char.id, "小星", full_reply)
        char.favorability += 1
        char.mood = round(max(-1.0, min(1.0, char.mood + 0.02)), 4)
        update_favor_stage(char)

        user = db.query(User).filter(User.id == user_id).first()
        if user:
            from datetime import datetime
            user.last_active = datetime.utcnow()

        db.commit()
        db.refresh(char)

        # 发送完成信号
        await ws.send_text(json.dumps({
            "type": "reply_done",
            "full_reply": full_reply,
            "favorability": char.favorability,
            "mood": round(char.mood, 2),
            "expression": char.expression,
            "favor_stage": char.favor_stage_name,
        }, ensure_ascii=False))

    finally:
        db.close()
