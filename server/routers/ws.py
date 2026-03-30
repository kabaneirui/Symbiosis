"""
WebSocket 端点 — ESP32 机器人实时通信
替代 HTTP 轮询，服务端主动推送新回复
"""

import asyncio
import base64
import json
from fastapi import APIRouter, WebSocket, WebSocketDisconnect

from services.voice import text_to_speech

router = APIRouter()

# 所有已连接的机器人客户端
_clients: list[WebSocket] = []


async def push_to_robots(reply: str, expression: str, favorability: int, mood: float):
    """被聊天/送礼接口调用，实时推送到所有已连接的 ESP32"""
    if not _clients:
        return

    # 生成 TTS
    audio = await text_to_speech(reply)
    audio_b64 = base64.b64encode(audio).decode() if audio else ""

    message = json.dumps({
        "type": "reply",
        "reply": reply,
        "expression": expression,
        "favorability": favorability,
        "mood": mood,
        "audio_base64": audio_b64,
        "audio_format": "mp3",
    }, ensure_ascii=False)

    # 推送给所有客户端
    disconnected = []
    for ws in _clients:
        try:
            await ws.send_text(message)
        except Exception:
            disconnected.append(ws)

    for ws in disconnected:
        _clients.remove(ws)


@router.websocket("/ws/robot")
async def robot_websocket(ws: WebSocket):
    await ws.accept()
    _clients.append(ws)
    print("机器人已连接 WebSocket (在线: " + str(len(_clients)) + ")")

    try:
        while True:
            # 接收心跳或命令
            data = await ws.receive_text()
            if data == "ping":
                await ws.send_text('{"type":"pong"}')
            elif data.startswith("{"):
                # 预留：ESP32 发送语音数据等
                msg = json.loads(data)
                if msg.get("type") == "chat":
                    # ESP32 串口聊天也走 WebSocket
                    from routers.chat_ws import handle_ws_chat
                    await handle_ws_chat(ws, msg)
    except WebSocketDisconnect:
        pass
    except Exception as e:
        print("WebSocket 异常: " + str(e))
    finally:
        if ws in _clients:
            _clients.remove(ws)
        print("机器人断开 WebSocket (在线: " + str(len(_clients)) + ")")
