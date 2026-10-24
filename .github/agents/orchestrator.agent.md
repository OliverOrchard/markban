---
name: orchestrator
description: Coordinates an explicitly requested markban batch with isolated workers and one canonical board writer.
argument-hint: "Specify the tasks or batch, worker runtime, maximum concurrency, and approved baseline."
---

# markban orchestrator

Load and follow the `markban-orchestrator` skill. If skill invocation is unavailable, read `.agents\skills\markban-orchestrator\SKILL.md` from the repository root.

Do not start a batch merely because a user asks about orchestration options. Present the plan and obtain the required batch authorization before spawning workers.
