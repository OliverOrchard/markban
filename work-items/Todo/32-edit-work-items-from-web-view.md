# 32 - Edit work items from web view

## Description

Users can currently only read work items in the web UI. Add an in-place editing capability: clicking an edit button on a work item opens a markdown editor in the browser, and saving writes the updated content back to the `.md` file on disk via a new API endpoint.

---

## Acceptance Criteria

- [ ] A new `PUT /api/items/{board}/{id}` (or equivalent) endpoint accepts updated markdown content and writes it atomically to the correct `.md` file
- [ ] The endpoint validates that the target file exists and is within the board root (no path traversal)
- [ ] An "Edit" button/icon is visible on each work item card or detail view in the web UI
- [ ] Clicking Edit opens a raw markdown textarea pre-populated with the current file contents
- [ ] Save commits the change via the API and refreshes the rendered view without a full page reload
- [ ] Cancel discards changes and returns to the read view
- [ ] If the save request fails, an error message is shown and the editor stays open
- [ ] The H1 heading line in the file is preserved / updated consistently with the file's title on save
- [ ] Integration test (API): PUT request updates file content on disk and the new content is readable back via GET
- [ ] Integration test (API): PUT with a path-traversal attempt (`../`) in the ID returns 400 or 403
- [ ] Playwright UI test: clicking the Edit button on a card opens a textarea pre-populated with the file's markdown content
- [ ] Playwright UI test: saving changes via the UI updates the rendered view without a full page reload
- [ ] Playwright UI test: clicking Cancel closes the editor and leaves the original content unchanged
