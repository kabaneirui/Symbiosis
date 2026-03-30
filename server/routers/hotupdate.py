from pathlib import Path

from fastapi import APIRouter, HTTPException
from fastapi.responses import FileResponse

router = APIRouter()

HOTUPDATE_DIR = Path(__file__).parent.parent / "hotupdate"


@router.get("/hotupdate/{filename}")
async def download_dll(filename: str):
    """提供热更新 DLL 下载（客户端启动时拉取最新版本）"""
    if not filename.endswith(".dll"):
        raise HTTPException(status_code=400, detail="只允许下载 .dll 文件")

    file_path = HOTUPDATE_DIR / filename
    if not file_path.exists():
        raise HTTPException(status_code=404, detail="热更文件不存在，请先上传 DLL")

    return FileResponse(
        path=str(file_path),
        filename=filename,
        media_type="application/octet-stream",
    )
