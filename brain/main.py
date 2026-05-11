"""
main.py — Ghost DevOps Brain API.

Wraps the LangGraph pipeline in a LangServe FastAPI endpoint so the .NET
Gateway can invoke the agent debate via a simple HTTP POST.

Endpoint: POST /ghost-devops/invoke
Body:     { "input": { "issue_description": "...", "logs": "...", "service": "..." } }
Response: { "output": { "plan": "...", "code_fix": "...", "is_safe": true, ... } }
"""

import os
from dotenv import load_dotenv
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from langserve import add_routes
from agents import graph

load_dotenv()

# ─────────────────────────────────────────────────────────────────────────────
app = FastAPI(
    title       = "Ghost DevOps Brain",
    description = "LangGraph multi-agent pipeline: Architect → Developer → Safety Validator",
    version     = "1.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins  = ["*"],
    allow_methods  = ["*"],
    allow_headers  = ["*"],
)

# ── Health check ──────────────────────────────────────────────────────────────
@app.get("/health")
def health():
    return {"status": "ok", "service": "ghost-devops-brain"}

# ── LangServe endpoint — exposes graph.invoke() over HTTP ────────────────────
add_routes(
    app,
    graph,
    path="/ghost-devops",
    input_type=dict,
    output_type=dict,
)

# ─────────────────────────────────────────────────────────────────────────────
if __name__ == "__main__":
    import uvicorn
    print("🧠 Ghost DevOps Brain starting...")
    print(f"📡 API available at http://0.0.0.0:8000/ghost-devops/invoke")
    print(f"📚 Docs at http://0.0.0.0:8000/docs")
    uvicorn.run(app, host="0.0.0.0", port=8000, log_level="info")
