# 45 - Undo for web view reorder

## Description

Dragging a card in the web view renumbers the work item files, so an accidental drag is easy to make and tedious to fix by hand. Add an undo for reorder so a mistake is one action to reverse, and make the feature configurable for teams that do not want it.

**Undo toast:** after every successful reorder, show a toast such as "Moved 35 above 38 -- Undo" for about 8 seconds. Clicking Undo restores the previous order.

**Keyboard:** `Ctrl+Z` undoes the last reorder and `Ctrl+Shift+Z` (also `Ctrl+Y`) redoes it, while the board has focus and no text field or dialog is active. The history is a per-session stack held in the browser; it is not persisted across reloads and is cleared when switching boards.

**Mechanism:** a reorder is a renumbering, so undo re-submits the previous order through the existing `POST /api/reorder` endpoint. Ordinary moves need no new server logic. Undoing a sub-item move made in `force` mode (see `subItemReorder`) must also re-attach the sub-item to its parent.

**Configuration** (explicit default written by `markban init`, like other settings):
```json
"web": {
  "reorderUndo": true
}
```
When `false`, no toast is shown, `Ctrl+Z` is ignored, and no history is kept. The value is served to the UI by an existing config endpoint.

Git remains the safety net for anything beyond the in-session undo stack.

---

## Acceptance Criteria

### Behaviour
- [ ] A successful reorder shows an undo toast that disappears after a short timeout
- [ ] Clicking Undo restores the exact previous order and file numbering
- [ ] `Ctrl+Z` undoes and `Ctrl+Shift+Z` / `Ctrl+Y` redoes the last reorder while the board has focus
- [ ] Shortcuts are ignored while typing in a text field or with a dialog open
- [ ] The history is cleared on board switch and on page reload
- [ ] Undoing a `force` sub-item move re-attaches the sub-item to its original parent
- [ ] A new reorder after an undo discards the redo history

### Configuration
- [ ] `web.reorderUndo` is parsed from `markban.json` and defaults to `true`
- [ ] `markban init` writes `reorderUndo` explicitly
- [ ] With `reorderUndo: false` there is no toast, no shortcut handling, and no history
- [ ] README documents the setting

### Tests
- [ ] API or unit test: undo (re-submitting the previous order) restores the original file numbering
- [ ] Playwright UI test: reorder, click Undo, and the original order returns
- [ ] Playwright UI test: `Ctrl+Z` then `Ctrl+Shift+Z` undoes and redoes a reorder
- [ ] Playwright UI test: with `reorderUndo: false` no toast appears and `Ctrl+Z` does nothing
