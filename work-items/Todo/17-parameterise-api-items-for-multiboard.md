# 17 - Parameterise /api/items for multiboard

## Description

Update `GET /api/items` and `POST /api/move` in `WebServer` to accept an optional `?board=<key>` query parameter. When present, the key is resolved to the corresponding board root path from the configured `boards` array and that path is used for `WorkItemStore.LoadAll()` and `MoveCommand.Execute()`. When absent, the default `rootPath` is used -- preserving existing single board behaviour.

---

## Acceptance Criteria

- [ ] `GET /api/items?board=backend` loads items from the correct board root
- [ ] `POST /api/move` with `?board=backend` moves within the correct board
- [ ] Unknown `board` key returns a `400` with a clear error
- [ ] Omitting `?board` falls back to default root -- no behaviour change for single board users
- [ ] Board key lookup is case-insensitive
