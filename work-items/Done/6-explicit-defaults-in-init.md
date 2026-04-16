# 6 - Explicit defaults in init

## Description

When `markban init` runs, it should write a `markban.json` that makes the default configuration fully explicit -- no invisible magic. Every lane, every default setting, written out. Users can read it, understand it, and edit it before or after init.

This also enables a powerful workflow: a user can create or edit `markban.json` first (customising lanes, path, etc.) and then run `markban init` to bootstrap the directories from that config. Init becomes a two-way operation -- it writes config if none exists, or reads existing config to know what to create.

Example of what `markban init` would write:

```json
{
  "rootPath": "./work-items",
  "lanes": [
    { "name": "Backlog",     "ordered": true },
    { "name": "Todo",        "ordered": true,  "type": "ready" },
    { "name": "In Progress", "ordered": true },
    { "name": "Done",        "ordered": true,  "type": "done" },
    { "name": "Ideas",       "ordered": false, "pickable": false },
    { "name": "Rejected",    "ordered": false, "pickable": false }
  ],
  "git": {
    "enabled": true
  },
  "web": {
    "port": 5000
  }
}
```

If `markban.json` already exists when `init` is run, init reads it and creates only the missing directories -- it never overwrites the config.

---

## Acceptance Criteria

- [x] `markban init` creates `markban.json` with all defaults written out explicitly
- [x] `markban init` with a pre-existing `markban.json` reads it and bootstraps directories accordingly
- [x] Custom lanes in a pre-existing config are respected -- correct directories are created
- [x] `--dry-run` shows both the config that would be written and the directories that would be created
- [x] Re-running init on an already-bootstrapped board is safe -- no overwrites
- [x] Depends on [init-command] (item 2) and [configurable-lanes] (item 4)
