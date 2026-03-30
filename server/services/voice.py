"""
语音服务 — 火山引擎豆包语音（TTS + STT）
备用：faster-whisper + Edge TTS
"""

import io
import json
import base64
import uuid
import tempfile

import httpx

from config import settings

# 火山引擎语音 API
TTS_URL = "https://openspeech.bytedance.com/api/v1/tts"
STT_URL = "https://openspeech.bytedance.com/api/v1/asr"

# TTS 音色（火山引擎）
VOLCANO_TTS_SPEAKER = "zh_female_qingxin"  # 清新女声
# 其他可选：zh_female_shuangkuai（爽快）、zh_male_chunhou（醇厚男声）

# 是否使用火山引擎（有 token 就用，没有就用免费方案）
def _use_volcano():
    return settings.voice_access_token and settings.voice_access_token != ""


async def text_to_speech(text: str) -> bytes:
    """文字转语音"""
    if _use_volcano():
        return await _volcano_tts(text)
    else:
        return await _edge_tts(text)


async def speech_to_text(audio_data: bytes, audio_format: str = "wav") -> str:
    """语音转文字"""
    if _use_volcano():
        return await _volcano_stt(audio_data, audio_format)
    else:
        return await _whisper_stt(audio_data, audio_format)


# ==================== 火山引擎实现 ====================

async def _volcano_tts(text: str) -> bytes:
    """火山引擎 TTS"""
    headers = {
        "Content-Type": "application/json",
        "Authorization": "Bearer;" + settings.voice_access_token,
    }
    payload = {
        "app": {
            "appid": settings.voice_app_id,
            "token": "access_token",
            "cluster": "volcano_tts",
        },
        "user": {"uid": "symbiosis_robot"},
        "audio": {
            "voice_type": VOLCANO_TTS_SPEAKER,
            "encoding": "mp3",
            "speed_ratio": 1.0,
            "volume_ratio": 1.0,
            "pitch_ratio": 1.0,
        },
        "request": {
            "reqid": str(uuid.uuid4()),
            "text": text,
            "text_type": "plain",
            "operation": "query",
        },
    }

    async with httpx.AsyncClient(timeout=10.0) as client:
        resp = await client.post(TTS_URL, json=payload, headers=headers)
        if resp.status_code == 200:
            data = resp.json()
            audio_b64 = data.get("data", "")
            if audio_b64:
                return base64.b64decode(audio_b64)
            print("火山TTS返回无音频:", json.dumps(data, ensure_ascii=False)[:200])
        else:
            print("火山TTS失败:", resp.status_code, resp.text[:200])

    # 失败则回退到 Edge TTS
    return await _edge_tts(text)


async def _volcano_stt(audio_data: bytes, audio_format: str = "wav") -> str:
    """火山引擎一句话识别"""
    headers = {
        "Content-Type": "application/json",
        "Authorization": "Bearer;" + settings.voice_access_token,
    }
    payload = {
        "app": {
            "appid": settings.voice_app_id,
            "token": "access_token",
            "cluster": "volcano_asr",
        },
        "user": {"uid": "symbiosis_robot"},
        "audio": {
            "format": audio_format,
            "codec": "raw",
            "rate": 16000,
            "bits": 16,
            "channel": 1,
            "language": "zh-CN",
        },
        "request": {
            "reqid": str(uuid.uuid4()),
            "sequence": -1,
        },
        "additions": {
            "with_frontend": "1",
        },
    }

    audio_b64 = base64.b64encode(audio_data).decode()
    payload["audio"]["data"] = audio_b64

    async with httpx.AsyncClient(timeout=15.0) as client:
        resp = await client.post(STT_URL, json=payload, headers=headers)
        if resp.status_code == 200:
            data = resp.json()
            results = data.get("result", [])
            if results:
                return results[0].get("text", "")
            # 有时结果在不同字段
            text = data.get("text", "")
            if text:
                return text
            print("火山STT返回:", json.dumps(data, ensure_ascii=False)[:300])
        else:
            print("火山STT失败:", resp.status_code, resp.text[:200])

    # 失败则回退到 Whisper
    return await _whisper_stt(audio_data, audio_format)


# ==================== 免费备用方案 ====================

async def _edge_tts(text: str) -> bytes:
    """Edge TTS 备用"""
    try:
        import edge_tts
        communicate = edge_tts.Communicate(text, "zh-CN-XiaoyiNeural")
        audio_bytes = io.BytesIO()
        async for chunk in communicate.stream():
            if chunk["type"] == "audio":
                audio_bytes.write(chunk["data"])
        return audio_bytes.getvalue()
    except Exception as e:
        print("Edge TTS 失败:", e)
        return b""


_whisper_model = None

async def _whisper_stt(audio_data: bytes, audio_format: str = "wav") -> str:
    """faster-whisper 备用"""
    global _whisper_model
    try:
        if _whisper_model is None:
            from faster_whisper import WhisperModel
            print("加载 Whisper 模型...")
            _whisper_model = WhisperModel("base", device="cpu", compute_type="int8")
            print("Whisper 加载完成")

        with tempfile.NamedTemporaryFile(suffix="." + audio_format, delete=True) as f:
            f.write(audio_data)
            f.flush()
            segments, _ = _whisper_model.transcribe(
                f.name, language="zh",
                initial_prompt="以下是普通话的句子。",
                vad_filter=True,
            )
            return "".join(seg.text for seg in segments).strip()
    except Exception as e:
        print("Whisper 失败:", e)
        return ""
