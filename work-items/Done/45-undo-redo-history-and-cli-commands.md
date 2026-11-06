# 45 - Undo/redo history and CLI commands

## Description

Reordering (and moving) work items renumbers or relocates files, so a mistake -- by a person in the web view or by an agent on the CLI -- is tedious to reverse by hand. Add a shared, persisted undo/redo history and CLI commands to use it. The web view undo (see [undo-for-web-view-reorder]) builds on the same history so the two stay consistent.

**History journal:** a small file per board, stored outside the work-item folders and gitignored (for example `.markban/history.json` next to `markban.json`). Each entry records the operation, a timestamp, and the before/after state of every affected file (path, number, and a content hash). Entries are only written by successful, non-dry-run operations.

**Commands:**
```
markban undo [--dry-run]
markban redo [--dry-run]
markban undo --list          # show the history stack; --list works with redo as well
```
- `undo` reverses the most recent entry and moves it onto the redo stack.
- `redo` re-applies the most recently undone entry.
- A new recorded operation clears the redo stack.
- `--dry-run` shows exactly which files would change.

**Safety (important for agents):** before undoing or redoing, verify that every affected file still matches the hash recorded for that entry. If anything changed since (edited, moved, or deleted by another operation), refuse with a clear error naming the files, and change nothing. Undo must never overwrite someone else's later work.

**Scope:** start with `reorder` and `move`. Commands that touch git (`commit`, `start`) or the board config (`lane rename`, `board remove`) are explicitly out of scope and are not recorded. Detaching a sub-item as part of a `force` reorder is recorded as part of that reorder entry so undo re-attaches it.

**Configuration:**
```json
"history": {
  "enabled": true,
  "maxEntries": 50
}
```
When disabled, nothing is recorded and `undo`/`redo` report that history is disabled. Oldest entries are dropped beyond `maxEntries`. `markban init` writes these defaults explicitly.

**Web API:** expose the same operations to the web UI (for example `POST /api/undo`, `POST /api/redo`, `GET /api/history`) so the undo toast and `Ctrl+Z` in the web view use this journal.

---

## Acceptance Criteria

### Journal
- [x] Successful `reorder` and `move` operations (CLI and web) append an entry; dry-runs and failures do not
- [x] The journal lives outside the work-item lanes and is gitignored (or documented as such)
- [x] `history.maxEntries` is honoured and the oldest entries are dropped
- [x] A corrupt or unreadable journal is reported clearly and never blocks normal commands

### Commands
- [x] `markban undo` restores the previous file numbering/location and moves the entry to the redo stack
- [x] `markban redo` re-applies an undone entry; a new operation clears the redo stack
- [x] `markban undo --list` and `markban redo --list` show the stacks
- [x] `--dry-run` on both previews the exact changes without writing
- [x] Undoing a `force` sub-item reorder re-attaches the sub-item to its original parent
- [x] Undo/redo with an empty stack exits non-zero with a clear message

### Safety
- [x] If any affected file changed since the entry was recorded, undo/redo refuses, names the files, exits non-zero, and changes nothing
- [x] Undo and redo are themselves not recorded as new undoable operations (they move entries between stacks)

### Configuration and API
- [x] `history.enabled` and `history.maxEntries` are parsed from `markban.json` with explicit defaults written by `markban init`
- [x] With `history.enabled: false` nothing is recorded and the commands explain why they do nothing
- [x] `POST /api/undo`, `POST /api/redo` and `GET /api/history` operate on the same journal
- [x] README documents the commands, the safety rule and the config

### Tests
- [x] Unit tests for journal write, undo, redo, stack clearing, max entries and the hash-mismatch refusal
- [x] Integration test: CLI `reorder` then `undo` then `redo` round-trips the board
- [x] Integration test: a CLI reorder can be undone through the web API
