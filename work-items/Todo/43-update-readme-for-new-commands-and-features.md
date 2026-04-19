# 43 - Update README for new commands and features

## Description

The README was written for the original command set. A large number of new commands and features have since been added and are planned. The README needs a comprehensive update before the next release to reflect the current and upcoming state of the tool.

This task should be done after the lane/board command group (34–39, 42) and the web UI improvements (30–33, 41) are shipped, so all new commands are documented accurately.

---

## Acceptance Criteria

- [ ] CLI reference section lists all current commands with correct syntax (including `lane rename/add/remove/reorder`, `board add/remove`)
- [ ] Web UI section documents the multi-board switcher, drag-to-reorder, in-place editing, and rich markdown rendering
- [ ] Configuration reference (`markban.json`) documents all supported keys: `lanes`, `boards`, `wipLimits`, `commitTags`, `commitMessageMaxLength`, `slugCasing`, `h1Heading`, `name`, lane-level `defaultCollapsed`
- [ ] Quick-start section reflects the current `markban init` → `markban create` → `markban web` onboarding flow
- [ ] Installation section covers all three channels: dotnet tool, Homebrew, winget (note winget as planned/upcoming)
- [ ] All example commands in the README are tested against the current release and produce the documented output
- [ ] No references to removed or renamed commands remain
