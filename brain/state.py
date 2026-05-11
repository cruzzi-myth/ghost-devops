"""
state.py — Shared state schema for the Ghost DevOps LangGraph agent pipeline.

The AgentState TypedDict is the single source of truth passed between all
three nodes (Architect, Developer, Safety). Every field is optional with
a default — LangGraph merges partial updates returned by each node.
"""

from typing import TypedDict, Annotated
import operator


class AgentState(TypedDict):
    # ── Input ─────────────────────────────────────────────────────────────────
    issue_description: str          # Summary from Prometheus alert annotation
    logs:              str          # Last 100 lines from the container
    service:           str          # e.g. "target-app"

    # ── Architect output ──────────────────────────────────────────────────────
    plan:              str          # High-level diagnosis + fix strategy
    architect_log:     str          # Full architect reasoning (for UI monologue)

    # ── Developer output ──────────────────────────────────────────────────────
    code_fix:          str          # Actual C# code patch
    developer_log:     str          # Full developer reasoning (for UI monologue)

    # ── Safety Validator ─────────────────────────────────────────────────────
    is_safe:           bool         # Did the safety check pass?
    safety_reason:     str          # Reason if rejected

    # ── Loop control ──────────────────────────────────────────────────────────
    iterations:        int          # Prevents infinite loops (max 3)
    feedback:          str          # Architect feedback to Developer on retry
