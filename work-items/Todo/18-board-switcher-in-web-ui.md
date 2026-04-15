# 18 - Board switcher in web UI

## Description

Add a board switcher `<select>` element to the web UI (`app.js` / `index.html`) that appears in the top right of the board. On load, the frontend calls `GET /api/boards` -- if the response is empty it hides the switcher entirely; if boards are returned it populates the select and shows it. Switching selection reloads items from `GET /api/items?board=<key>` without a full page refresh.

---

## Acceptance Criteria

- [ ] Switcher is hidden when `GET /api/boards` returns `[]`
- [ ] Switcher is visible and populated when boards are returned
- [ ] Selecting a board reloads the kanban columns with that board's items
- [ ] Selected board is preserved on manual page refresh (localStorage)
- [ ] Move operations pass the active board key so moves go to the correct board
- [ ] Styling is consistent with existing UI
