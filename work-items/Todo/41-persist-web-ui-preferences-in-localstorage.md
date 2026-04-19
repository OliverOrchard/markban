# 41 - Persist web UI preferences in localStorage

## Description

UI display preferences in the web view are currently lost on every board switch and page refresh. Preferences should be persisted using a two-tier model:

1. **Server defaults** -- optional per-lane config in `markban.json` (e.g. `"defaultCollapsed": true`) served via the existing `/api/lanes` response. Read once on page load as the initial value.
2. **Personal overrides** -- stored in `localStorage` under a per-board key. Written whenever the user changes a preference. Always takes precedence over the server default.

Resolution order on page load:
```
effective state = localStorage value ?? server default ?? hardcoded fallback
```

This means board owners can set sensible defaults for all users (e.g. Ideas collapsed), while individuals can freely override without affecting anyone else. Clearing localStorage resets to the board defaults.

**localStorage key scheme** (namespaced to avoid collisions):
```
markban:board-prefs:<board-key>   -> JSON object with all per-board prefs
markban:global-prefs              -> JSON object with cross-board prefs
```

Storing all prefs for a board in a single JSON object (rather than one key per pref) makes it easy to add new preferences in the future without proliferating keys.

---

## Acceptance Criteria

### Per-board preferences (stored under `markban:board-prefs:<board-key>`)
- [ ] Hidden/visible state of each lane is persisted per board
- [ ] Switching boards restores the hidden lane state for the incoming board
- [ ] Switching back to a previous board restores its previously saved lane visibility -- the original bug this task fixes

### Global preferences (stored under `markban:global-prefs`)
- [ ] Card density setting (compact = title+ID only / full = with description preview) is persisted globally and applied across all boards

### Server defaults (two-tier model)
- [ ] Lane config in `markban.json` supports an optional `"defaultCollapsed": true` property per lane
- [ ] `/api/lanes` response includes the `defaultCollapsed` value for each lane
- [ ] On first load (no localStorage entry), the UI applies the server default for each lane
- [ ] A user override in localStorage takes precedence over the server default
- [ ] Clearing localStorage resets the UI to server defaults

### Storage hygiene
- [ ] All localStorage keys are prefixed with `markban:` to avoid collisions with other apps on the same origin
- [ ] Corrupted or unparseable localStorage values are silently ignored and fall back to defaults (no console errors or broken UI)

### Tests
- [ ] Playwright UI test: hiding a lane, switching boards, and switching back restores the hidden state
- [ ] Playwright UI test: compact density setting survives a page reload
- [ ] Playwright UI test: a lane with `defaultCollapsed: true` in config renders collapsed on first load (no localStorage entry)
