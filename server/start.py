import os
import uvicorn

port = int(os.environ.get("PORT", 8000))
print("启动端口:", port)
uvicorn.run("main:app", host="0.0.0.0", port=port)
