# 33 - Drag-to-reorder work items in web view

## Description

Users can reorder work items within a lane via the CLI `reorder` command. Expose the same capability in the web UI using drag-and-drop: dragging a card to a new position within its lane triggers the reorder logic and persists the new order to disk.

---

## Acceptance Criteria

- [ ] Work item cards within a lane are draggable using native HTML5 drag-and-drop (no external DnD library required)
- [ ] Dropping a card at a new position within the same lane triggers a `POST /api/reorder` (or equivalent) API call with the new ordered list of IDs for that lane
- [ ] The server-side handler calls the existing `ReorderCommand` logic (no duplication of reorder business logic)
- [ ] The new file order on disk matches the order the user dropped items into
- [ ] The UI updates optimistically on drop; if the server call fails the original order is restored and an error is shown
- [ ] Dragging across lanes is explicitly out of scope for this item (cross-lane drag = move, tracked separately)
- [ ] A visual drop-target indicator is shown while dragging
- [ ] Integration test (API): POST reorder request updates file names on disk to reflect new priority order
- [ ] Playwright UI test: dragging the second card above the first card in a lane results in the correct new order being reflected in the DOM after the drop
- [ ] Playwright UI test: after a successful drag-drop the API has persisted the new order (verify via a subsequent GET /api/items response)
- [ ] Playwright UI test: a failed reorder API call (simulated) restores the original card order in the UI
- [ ] Existing drag-unrelated tests are unaffected
