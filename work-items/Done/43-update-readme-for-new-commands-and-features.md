# 43 - Update README for new commands and features

## Description

The README is 287 lines and has grown bloated for an open-source project. Model it on https://github.com/herdrdev/herdr: a tagline, a demo, a handful of benefit bullets, install, a short quick start, and links out. Move the detail into a `docs/` folder with one file per topic.

Scope (revised): slim README plus `docs/` text. Demo media (web UI video and a short CLI GIF) is a follow-up item once the final web UI and commands are integrated; leave clearly marked placeholders in the README for them.

---

## Acceptance Criteria

- [x] README is about 60-80 lines: tagline, media placeholders, ~6 benefit bullets, install (dotnet tool, Homebrew, winget noted as planned), 4-command quick start (`init`, `create`, `list`/`next`, `web`), docs links, development, license
- [x] `docs/` contains one file per topic: commands (full CLI reference with correct syntax, including `lane rename/add`, `board add/remove`, `undo/redo`), configuration (every `markban.json` key), web UI, AI agents (including the repository agent harness), and item template
- [x] Commands that are in Testing (`lane add`, `board add`, `undo`, `redo`, `undo --list`, `history` settings) are documented from their worktrees, not invented
- [x] No content is lost: every section removed from the README exists in `docs/`
- [x] Every example command is run against a current build and its documented output matches
- [x] No references to removed or renamed commands remain; all relative links resolve
