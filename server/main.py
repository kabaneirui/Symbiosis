from pathlib import Path

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles

from database import init_db
from routers import user, chat, gift, state, hotupdate, memory, events, voice, robot, ws, shop, stream

app = FastAPI(
    title="Symbiosis - AI 陪伴机器人后端",
    description="AI 陪伴机器人养成游戏后端服务",
    version="0.1.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(user.router, tags=["用户"])
app.include_router(chat.router, tags=["聊天"])
app.include_router(gift.router, tags=["送礼"])
app.include_router(state.router, tags=["状态"])
app.include_router(memory.router, tags=["记忆"])
app.include_router(events.router, tags=["事件"])
app.include_router(voice.router, tags=["语音"])
app.include_router(robot.router, tags=["机器人"])
app.include_router(shop.router, tags=["商店"])
app.include_router(stream.router, tags=["流式聊天"])
app.include_router(ws.router, tags=["WebSocket"])
app.include_router(hotupdate.router, tags=["热更新"])

# H5 静态文件托管
static_dir = Path(__file__).parent / "static"
if static_dir.exists():
    app.mount("/h5", StaticFiles(directory=str(static_dir), html=True), name="h5")


@app.on_event("startup")
def on_startup():
    init_db()
    print("数据库初始化完成")
    print("服务启动成功 → http://127.0.0.1:8000/docs")
    print("H5 手机版 → http://127.0.0.1:8000/h5/")


@app.get("/")
def root():
    return {"status": "ok", "message": "Symbiosis 后端服务运行中"}
