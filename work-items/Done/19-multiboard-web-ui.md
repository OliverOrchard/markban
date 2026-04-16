# 19 - Multiboard web UI

## Description

Top-level tracking item for the full multiboard feature. Users with multiple boards in a repo can configure them in `markban.json` and switch between them in the web UI via a select box in the top right corner of the board.

Boards are identified by a `boards` array in `markban.json`. Each entry has a `name` and a `path` (relative to the config file) pointing to the directory that contains the board (e.g. the folder holding `work-items/`). When no `boards` key is present, the web UI behaves exactly as today -- fully backward compatible.

See sub-items [parse-boards-config], [add-api-boards-endpoint], [parameterise-api-items-for-multiboard], [board-switcher-in-web-ui] for implementation slices.

---

## Acceptance Criteria

- [x] `markban.json` `boards` array is the sole discovery mechanism -- no filesystem scanning
- [x] Absence of `boards` key = single board mode, no UI changes
- [x] Board names in the switcher come from the `name` field in each entry
- [x] Switching boards reloads items without a full page refresh
- [x] Works alongside all existing CLI commands unchanged
