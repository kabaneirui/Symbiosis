import json
from typing import AsyncGenerator

import httpx

from config import settings


async def call_llm(system_prompt: str, user_message: str) -> str:
    """非流式调用 LLM（兼容现有接口）"""

    if settings.llm_api_key == "your_api_key_here":
        return _mock_reply(user_message)

    url = f"{settings.llm_base_url}/chat/completions"
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {settings.llm_api_key}",
    }
    payload = {
        "model": settings.llm_model,
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": user_message},
        ],
        "temperature": 0.8,
        "max_tokens": 300,
    }

    async with httpx.AsyncClient(timeout=30.0) as client:
        resp = await client.post(url, json=payload, headers=headers)
        resp.raise_for_status()
        data = resp.json()
        return data["choices"][0]["message"]["content"]


async def call_llm_stream(system_prompt: str, user_message: str) -> AsyncGenerator[str, None]:
    """流式调用 LLM — 逐句返回文本片段（SSE）"""

    if settings.llm_api_key == "your_api_key_here":
        yield _mock_reply(user_message)
        return

    url = f"{settings.llm_base_url}/chat/completions"
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {settings.llm_api_key}",
    }
    payload = {
        "model": settings.llm_model,
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": user_message},
        ],
        "temperature": 0.8,
        "max_tokens": 300,
        "stream": True,
    }

    async with httpx.AsyncClient(timeout=30.0) as client:
        async with client.stream("POST", url, json=payload, headers=headers) as resp:
            resp.raise_for_status()
            async for line in resp.aiter_lines():
                if not line.startswith("data: "):
                    continue
                data_str = line[6:]
                if data_str.strip() == "[DONE]":
                    break
                try:
                    chunk = json.loads(data_str)
                    delta = chunk["choices"][0].get("delta", {})
                    content = delta.get("content", "")
                    if content:
                        yield content
                except (json.JSONDecodeError, KeyError, IndexError):
                    continue


def _mock_reply(user_message: str) -> str:
    return f"[Mock] 你说了「{user_message}」，我收到啦～"
