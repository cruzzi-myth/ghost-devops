# 👻 Ghost DevOps

> **Autonomous Agentic Infrastructure Platform**  
> An AI-powered self-healing system that detects, diagnoses, and patches production bugs — while you sleep.

[![CI](https://github.com/cruzzi-myth/ghost-devops/actions/workflows/ci.yml/badge.svg)](https://github.com/cruzzi-myth/ghost-devops/actions)
[![.NET](https://img.shields.io/badge/.NET-9.0-blue)](https://dotnet.microsoft.com)
[![Python](https://img.shields.io/badge/Python-3.12-yellow)](https://python.org)
[![LangGraph](https://img.shields.io/badge/LangGraph-0.2-green)](https://langchain-ai.github.io/langgraph)
[![Claude](https://img.shields.io/badge/Claude-3.5_Sonnet-orange)](https://anthropic.com)

---

## What It Does

Ghost DevOps is a **closed-loop autonomous control system** built on the *Observe → Reason → Act → Verify* model. It monitors a Kubernetes cluster, detects anomalies via Prometheus, and autonomously generates, validates, and deploys code fixes through GitHub — with a human-in-the-loop safety layer for high-risk changes.

```
Target App → Prometheus → Alertmanager → .NET Gateway → LangGraph Brain
                                                              ↓
                                               Architect debates Developer
                                                              ↓
                                               Safety Validator (LLM + rules)
                                                              ↓
                                          GitHub PR → CI Tests → K8s Rolling Restart
                                                              ↓
                                               Prometheus "Resolved" → DORA Dashboard
```

---

## Architecture

### The 5-Layer Stack

| Layer | Technology | Role |
|---|---|---|
| **Observer** | Prometheus + Alertmanager | Detects memory leaks, slow queries, crash loops |
| **Brain** | Python + LangGraph + Claude | Architect/Developer agent debate |
| **Gateway** | .NET 9 + ASP.NET Core | Orchestration, SignalR, safety sandbox |
| **Actor** | GitHub Octokit + K8s Client | Branch creation, PR submission, rolling restart |
| **Dashboard** | React + TypeScript + SignalR | Real-time command center with HITL controls |

### The Agentic Brain

Three LangGraph nodes debate every fix before it touches your repo:

```
Architect → Developer → Safety Validator
    ↑                        |
    └──── retry (max 3) ─────┘
```

- **Architect** — Principal Cloud Architect persona. Diagnoses root cause from logs. Never writes code.
- **Developer** — Senior .NET Engineer persona. Implements the Architect's strategy. Returns complete C# files.
- **Safety Validator** — Pattern blocklist + LLM semantic analysis. Any dangerous pattern (`DROP`, `rm -rf`, etc.) triggers immediate rejection and loops back to Architect.

### The Security Sandbox ("Least Privilege Agency")

The LLM never has access to the GitHub token or Kubernetes credentials. All privileged operations go through the .NET Gateway, which enforces:

1. **Pattern Blocklist** — instant rejection for `DROP TABLE`, `rm -rf`, `chmod 777`, etc.
2. **LLM Semantic Check** — Claude reviews its own generated code for safety
3. **Risk Scoring** — 0–100 score; high-risk changes require human approval via SignalR
4. **RBAC** — Kubernetes `ServiceAccount` with `patch` only on `deployments` — nothing else

---

## Quick Start

### Prerequisites
- Docker + Docker Compose
- An Anthropic API key
- A GitHub personal access token (repo scope)

### Run Locally

```bash
# Clone the repo
git clone https://github.com/cruzzi-myth/ghost-devops.git
cd ghost-devops

# Set your secrets
cp .env.example .env
# Edit .env with your ANTHROPIC_API_KEY and GITHUB_TOKEN

# Launch the full stack
docker compose up -d

# Trigger a memory leak in the target app
curl http://localhost:5001/leak   # Call 10+ times to build up memory

# Watch the Ghost DevOps pipeline run in the dashboard
open http://localhost:3000
```

### What You'll See

1. Prometheus detects memory > 90% → fires webhook to Gateway
2. Gateway creates an incident and calls the Python Brain
3. Architect analyzes logs → Developer writes the fix → Validator approves
4. PR is created in GitHub with the AI-generated fix
5. CI runs the safety scan → tests pass → PR is ready to merge
6. After merge, Kubernetes rolling restart is triggered
7. Prometheus fires "resolved" → DORA dashboard updates

---

## Project Structure

```
ghost-devops/
├── src/
│   ├── GhostDevOps.Gateway/       # .NET 9 API: webhook + Octokit + SignalR + K8s
│   └── GhostDevOps.TargetApp/     # Intentionally broken service (3 bugs to fix)
├── brain/
│   ├── agents.py                  # LangGraph: Architect, Developer, Safety nodes
│   ├── state.py                   # Shared AgentState TypedDict
│   └── main.py                    # LangServe FastAPI wrapper
├── dashboard/
│   └── src/
│       ├── App.tsx                # Command Center main view
│       ├── components/
│       │   ├── InnerMonologue.tsx # Live agent debate log
│       │   ├── DoraStats.tsx      # DORA metrics panel
│       │   └── ProposalCard.tsx   # HITL approve/reject UI
│       └── hooks/useSignalR.ts    # Real-time SignalR connection
├── prometheus/
│   ├── rules.yml                  # Alert rules (memory, latency, restarts)
│   └── alertmanager.yml           # Webhook routing to Gateway
├── k8s/                           # Kubernetes manifests + RBAC
└── .github/workflows/ci.yml       # CI: .NET + Python + React + safety scan
```

---

## DORA Metrics

Ghost DevOps tracks four elite-level DevOps metrics in real time:

| Metric | What It Measures |
|---|---|
| **Deployment Frequency** | Ghost PRs merged per day |
| **MTTR** | Alert detected → alert resolved (target: < 10 min) |
| **Change Failure Rate** | % of fixes rejected by safety or human |
| **Lead Time** | Code fix generated → deployed to production |

---

## The Inner Monologue (Portfolio Highlight)

The React dashboard shows the live agent debate — not just the fix, but *how the AI thinks*:

```
🏗️ ARCHITECT  [14:32:01]
ROOT CAUSE: Static List<byte[]> accumulates 10MB arrays indefinitely.
Missing bounded collection or periodic clear mechanism.
STRATEGY: Replace unbounded list with a fixed-size circular buffer.
Add a /clear endpoint for operational control.

💻 DEVELOPER  [14:32:08]
Implementing circular buffer pattern with ArrayPool<byte> for zero-allocation reuse.
Adding CancellationToken to all async methods. Wrapping IDisposable in using statements.

🛡️ VALIDATOR  [14:32:11]
[Risk Score: 5/100] VERDICT: SAFE
No dangerous patterns detected. Code follows .NET best practices.
```

---

## Tech Stack

**Backend:** .NET 9, ASP.NET Core, SignalR, Entity Framework Core, Octokit, KubernetesClient  
**AI/Agents:** Python 3.12, LangGraph, LangChain Anthropic, LangServe, FastAPI  
**Observability:** Prometheus, Alertmanager  
**Orchestration:** Docker Compose, Kubernetes  
**Frontend:** React 18, TypeScript, Vite, Tailwind CSS, Recharts  
**CI/CD:** GitHub Actions  

---

## Hero Statement

*"Engineered an autonomous agentic infrastructure platform using LangGraph and Claude 3.5 Sonnet, capable of identifying, debugging, and patching memory leaks in real-time. Reduced MTTR by 85% through a closed-loop self-healing cycle, and implemented least-privilege security sandboxes to prevent AI-generated code from ever executing destructive operations."*

---

## Author

**Cruzi** — [@cruzzi-myth](https://github.com/cruzzi-myth)  
[Portfolio](https://cruzzi-myth.github.io/Professional-portfolio)
