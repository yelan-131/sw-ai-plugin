from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
import uvicorn
from sw_command_parser import parse_command
from config_manager import load_config, save_config, get_api_key

app = FastAPI(title="SW AI Backend")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"]
)

class ChatRequest(BaseModel):
    message: str

class ChatResponse(BaseModel):
    reply: str
    commands: list[dict]

class ConfigRequest(BaseModel):
    anthropic_api_key: str = ""

@app.get("/api/health")
async def health():
    return {"status": "ok"}

@app.post("/api/chat", response_model=ChatResponse)
async def chat(req: ChatRequest):
    result = parse_command(req.message)
    return ChatResponse(reply=result["reply"], commands=result["commands"])

@app.get("/api/config")
async def get_config():
    config = load_config()
    key = config.get("anthropic_api_key", "")
    masked = key[:8] + "..." + key[-4:] if len(key) > 12 else ("已设置" if key else "未设置")
    return {"anthropic_api_key_set": bool(key), "masked_key": masked}

@app.post("/api/config")
async def update_config(req: ConfigRequest):
    if req.anthropic_api_key:
        save_config({"anthropic_api_key": req.anthropic_api_key})
    return {"status": "ok", "message": "配置已保存"}

if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=8765)
