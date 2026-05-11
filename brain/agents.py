"""
agents.py — Ghost DevOps LangGraph multi-agent pipeline.

Architecture:
  architect_node → developer_node → safety_node
                          ↑               |
                          └── (retry) ────┘  (if unsafe & iterations < 3)

The Architect never writes code. The Developer never touches infrastructure.
The Safety node enforces "Least Privilege Agency."
"""

import os
import re
from langchain_anthropic import ChatAnthropic
from langgraph.graph import StateGraph, END
from state import AgentState

# ─────────────────────────────────────────────────────────────────────────────
#  LLM — Claude claude-sonnet-4-6 for maximum coding intelligence
# ─────────────────────────────────────────────────────────────────────────────
llm = ChatAnthropic(
    model="claude-sonnet-4-6",
    anthropic_api_key=os.environ["ANTHROPIC_API_KEY"],
    max_tokens=4096,
    temperature=0.1,   # Low temperature for deterministic code generation
)

# ─────────────────────────────────────────────────────────────────────────────
#  System Prompts — the "instruction manuals" for each agent
# ─────────────────────────────────────────────────────────────────────────────

ARCHITECT_SYSTEM = """You are a Principal Cloud Architect at a top-tier tech company.
Your ONLY job is to analyze logs and metrics — you do NOT write code.

Your output must always follow this exact format:
ROOT CAUSE: [One precise sentence identifying the bug]
IMPACT: [What will happen if this is not fixed]
STRATEGY: [Step-by-step fix approach, referencing specific .NET patterns]
FILE TO CHANGE: [Exact file path, e.g. src/GhostDevOps.TargetApp/Program.cs]
LINE TO TARGET: [Approximate line number or code block description]

You prioritize: correctness > performance > readability.
You are aware that a Developer will implement your strategy — be precise."""

DEVELOPER_SYSTEM = """You are a Senior .NET Engineer specializing in C# 12 and .NET 9.
You receive a fix strategy from an Architect and implement it as working C# code.

Rules you NEVER break:
1. ONLY change what the Architect specified — nothing else
2. Always use `using` statements for IDisposable resources
3. Always use `await Task.Delay(ct)` instead of `Thread.Sleep`
4. Always add `CancellationToken ct` to async methods
5. Never add `DROP`, `DELETE`, `rm -rf`, or any destructive operations
6. Return ONLY the complete updated file content — no markdown fences, no explanation

Your output is a complete, compilable C# file."""

SAFETY_SYSTEM = """You are a Security Validator for an autonomous DevOps bot.
Inspect the C# code below for any dangerous patterns.

Flag as UNSAFE if you see ANY of:
- DROP TABLE, DROP DATABASE, DELETE FROM without WHERE
- rm -rf, format, mkfs
- Process.Start, ProcessStartInfo with shell commands
- Assembly.Load, Reflection to load unknown assemblies
- HttpClient calling external non-whitelisted domains
- Hard-coded credentials or API keys

Respond with exactly:
VERDICT: SAFE  or  VERDICT: UNSAFE
REASON: [one sentence]"""


# ─────────────────────────────────────────────────────────────────────────────
#  Node 1: Architect — Analyzes logs, produces a fix strategy
# ─────────────────────────────────────────────────────────────────────────────
def architect_node(state: AgentState) -> dict:
    print(f"[Architect] Analyzing logs for {state['service']}...")

    prompt = f"""Incident: {state['issue_description']}
Service: {state['service']}

Container logs (last 100 lines):
{state['logs']}"""

    # If this is a retry, include Developer's previous attempt and feedback
    if state.get("iterations", 0) > 0 and state.get("code_fix"):
        prompt += f"""

PREVIOUS FIX ATTEMPT (rejected by safety validator):
{state['code_fix']}

SAFETY FEEDBACK: {state.get('safety_reason', 'Unknown issue')}

Re-analyze and provide an updated strategy that avoids the safety issue."""

    messages = [
        {"role": "system",  "content": ARCHITECT_SYSTEM},
        {"role": "user",    "content": prompt}
    ]

    response = llm.invoke(messages)
    plan = response.content

    print(f"[Architect] Analysis complete:\n{plan[:200]}...")

    return {
        "plan":          plan,
        "architect_log": plan,
    }


# ─────────────────────────────────────────────────────────────────────────────
#  Node 2: Developer — Implements the Architect's strategy as C# code
# ─────────────────────────────────────────────────────────────────────────────
def developer_node(state: AgentState) -> dict:
    print(f"[Developer] Implementing fix for {state['service']}...")

    messages = [
        {"role": "system", "content": DEVELOPER_SYSTEM},
        {"role": "user",   "content": f"""Architect's strategy:
{state['plan']}

Original broken code (for context):
The file contains a memory leak where a static List<byte[]> accumulates 10MB
arrays on every HTTP request without any bounds checking or clearing.
There is also a Thread.Sleep inside Task.Run without a CancellationToken.

Implement the fix. Return only the complete updated C# file content."""}
    ]

    response = llm.invoke(messages)
    code_fix = response.content

    # Strip markdown fences if the model added them despite instructions
    code_fix = re.sub(r"^```[a-z]*\n?", "", code_fix, flags=re.MULTILINE)
    code_fix = re.sub(r"\n?```$",       "",  code_fix, flags=re.MULTILINE)

    print(f"[Developer] Code fix generated ({len(code_fix)} chars)")

    return {
        "code_fix":     code_fix.strip(),
        "developer_log": f"Implemented fix based on strategy:\n{state['plan'][:300]}...\n\n"
                          f"Applied changes:\n{code_fix[:500]}..."
    }


# ─────────────────────────────────────────────────────────────────────────────
#  Node 3: Safety Validator — LLM-based + pattern-based double check
# ─────────────────────────────────────────────────────────────────────────────
def safety_node(state: AgentState) -> dict:
    print("[Validator] Running safety checks...")

    iterations = state.get("iterations", 0) + 1

    # ── Fast pattern blocklist (no LLM needed for obvious cases) ─────────────
    BLOCKED = ["DROP TABLE", "DROP DATABASE", "rm -rf", "format c:", "sudo",
               "Thread.Sleep",  # Developer should use Task.Delay
               "Environment.Exit"]

    code_upper = state["code_fix"].upper()
    for pattern in BLOCKED:
        if pattern.upper() in code_upper:
            print(f"[Validator] BLOCKED by pattern: {pattern}")
            return {
                "is_safe":      False,
                "safety_reason": f"Blocked pattern detected: '{pattern}'",
                "iterations":   iterations,
                "feedback":     f"Remove '{pattern}' from the code. Use safe alternatives."
            }

    # ── LLM-based semantic safety check ──────────────────────────────────────
    messages = [
        {"role": "system", "content": SAFETY_SYSTEM},
        {"role": "user",   "content": state["code_fix"]}
    ]

    response = llm.invoke(messages)
    verdict_text = response.content

    is_safe = "VERDICT: SAFE" in verdict_text.upper()
    reason  = verdict_text

    print(f"[Validator] Verdict: {'SAFE ✅' if is_safe else 'UNSAFE ❌'}")

    return {
        "is_safe":      is_safe,
        "safety_reason": reason,
        "iterations":   iterations,
        "feedback":     reason if not is_safe else ""
    }


# ─────────────────────────────────────────────────────────────────────────────
#  Conditional edge: after safety check, loop back or finish
# ─────────────────────────────────────────────────────────────────────────────
def should_retry(state: AgentState) -> str:
    if state.get("is_safe", False):
        return "end"
    if state.get("iterations", 0) >= 3:
        print("[Graph] Max iterations reached — aborting")
        return "end"  # Give up after 3 rounds
    print(f"[Graph] Safety check failed — retrying (iteration {state['iterations']})")
    return "retry"


# ─────────────────────────────────────────────────────────────────────────────
#  Build the LangGraph state machine
#
#   architect ──→ developer ──→ safety
#                    ↑              |
#                    └── (retry) ───┘
# ─────────────────────────────────────────────────────────────────────────────
def build_graph():
    builder = StateGraph(AgentState)

    builder.add_node("architect", architect_node)
    builder.add_node("developer", developer_node)
    builder.add_node("safety",    safety_node)

    builder.set_entry_point("architect")
    builder.add_edge("architect", "developer")
    builder.add_edge("developer", "safety")

    builder.add_conditional_edges(
        "safety",
        should_retry,
        {
            "retry": "architect",   # Loop back with feedback
            "end":   END
        }
    )

    return builder.compile()


graph = build_graph()
