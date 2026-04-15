# 15 - Parse boards config

## Description

Extend `WorkItemStore` (or a new config helper) to read a `boards` array from `markban.json` and resolve each entry to an absolute board root path.

Config shape:

```json
{
  "boards": [
    { "name": "Backend", "path": "services/api" },
    { "name": "Frontend", "path": "services/web" }
  ]
}
```

`path` is relative to the `markban.json` file location. Each resolved path is the directory passed to `WorkItemStore.LoadAll()` -- i.e. the folder that contains `work-items/` (or a custom `rootPath` if that board has its own config). Boards with no config file are supported -- the path simply points directly to their board root.

---

## Acceptance Criteria

- [ ] Parses `boards` array from `markban.json` when present
- [ ] Each `path` is resolved relative to the config file, not CWD
- [ ] Returns `null`/empty when no `boards` key exists (single board mode)
- [ ] Invalid or missing paths produce a clear error message
- [ ] Does not require sub-boards to have their own `markban.json`
