# 16 - Add /api/boards endpoint

## Description

Add a `GET /api/boards` endpoint to `WebServer` that returns the list of configured boards so the frontend knows whether to show the switcher and what options to populate it with.

Response shape:

```json
[
  { "name": "Backend", "key": "backend" },
  { "name": "Frontend", "key": "frontend" }
]
```

`key` is a stable identifier used as a query param on `/api/items?board=backend`. When no `boards` array is configured, the endpoint returns an empty array -- the frontend treats this as single board mode and hides the switcher.

---

## Acceptance Criteria

- [x] `GET /api/boards` returns the configured board list
- [x] Returns `[]` when no `boards` key in config (not a 404)
- [x] `key` values are URL-safe and stable across restarts
- [x] No-cache headers consistent with `/api/items`
