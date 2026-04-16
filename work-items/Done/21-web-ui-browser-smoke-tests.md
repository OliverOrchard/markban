# 21 - Web UI browser smoke tests

## Description

Add a small open-source browser automation layer for the local web UI so critical user flows are exercised in a real browser, not just through HTTP endpoint tests. Keep the scope intentionally small: this is smoke coverage for the hand-written UI, not snapshot-heavy testing.

Use an approach that fits the project's current philosophy:
- stays in the .NET/xUnit test workflow
- does not add an npm build pipeline
- runs against the real `markban web` server
- focuses on meaningful user journeys

---

## Acceptance Criteria

- [x] Uses an open-source browser automation tool appropriate for a .NET/xUnit repo
- [x] Covers single-board mode hiding the board switcher
- [x] Covers board switching reloading visible items in the UI
- [x] Covers board selection persisting across manual refresh
- [x] Covers a move action from the UI using the active board
- [x] Avoids brittle snapshot testing
