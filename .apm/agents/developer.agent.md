---
name: developer
description: Implements one markban board task using the shared markban-developer skill.
argument-hint: "Describe a feature or bug, say 'pick up next', or provide an orchestrator assignment."
---

# markban developer

Load and follow the `markban-developer` skill. If skill invocation is unavailable, read `.agents\skills\markban-developer\SKILL.md` from the repository root.

Use assigned worker mode when given a coordinator assignment; otherwise use the standalone workflow. Follow the current runtime's model, tool, and permission settings rather than assuming a particular editor or model.
