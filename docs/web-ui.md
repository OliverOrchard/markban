# Web UI

`markban web` starts a local Kestrel web server (default `http://localhost:5000`) and opens your browser to the board:

```bash
markban web [--port <port>] [--no-open]
```

The CLI and web UI can run simultaneously -- an agent or script can run `markban` CLI commands while you watch the board in the browser. Both read and write the same Markdown files in `work-items/` and never lock each other out.

## Features

### Multi-board switcher

When `boards` is configured in [`markban.json`](configuration.md#multi-board-example), the header displays a board selector dropdown. Switching boards reloads the lanes and items for the selected board without restarting the server, and remembers your last selected board in `localStorage`.

### Drag-to-reorder and drag-to-move

- **Move between lanes:** Drag a card from one lane column and drop it into another lane to move the file on disk (equivalent to `markban move`).
- **Reorder within a lane:** Drag a card vertically within an ordered lane to change its priority and renumber the lane's files on disk (equivalent to `markban reorder`).
- **Sub-item reorder policy:** Reordering a lettered sub-item (`12a`) follows the board's `subItemReorder` setting in `markban.json`:
  - `"warn"` (default): prompts for confirmation before detaching the sub-item from its parent and renumbering it as a standalone item.
  - `"block"`: locks sub-item cards against drag-reorder and shows a tooltip explaining the policy.
  - `"force"`: detaches and renumbers the sub-item immediately without prompting.

### In-place editing

Click any card to open its detail pane:
- Click **Edit** to open the raw Markdown editor in place.
- Click **Save** to write the updated Markdown atomically to disk and re-render the item immediately, or **Cancel** / `Esc` to close without saving.
- Use the **Move to...** dropdown in the detail header to move the item to another lane directly from the detail view.
- URL hash navigation (`#<slug>`) deep-links directly to a work item's detail pane.

### Rich Markdown and Mermaid rendering

Work-item bodies in the detail pane are rendered as GitHub Flavored Markdown:
- **Syntax-highlighted code blocks** via `highlight.js`.
- **Mermaid diagrams:** Fenced ```mermaid code blocks render as inline SVG diagrams and automatically adapt when switching between light and dark themes.
- **Clickable `[slug]` cross-references:** Any `[slug]` matching a work item on the board renders as an interactive link that opens the target item's detail pane.

### Filtering, tags, and blocked badges

- **Search bar:** Filters visible cards live by ID, slug, or full Markdown content.
- **Lane filter bar:** Toggle individual lanes on or off in the toolbar, with live item counts and blocked-item indicators per column.
- **Tag and blocked badges:** Cards display `blocked` badges (with the block reason on hover) and clickable `tags` badges. Clicking a tag filters the board to items carrying that tag.

### Cycle-time reports

Click **Reports** (`/reports/cycle-time`) in the header to view cycle-time metrics computed from git history for completed work items.

## Themes

Use the **Theme** control in the header (or in the detail pane) to choose **Light**, **Dark**, or **System**.

- **System** is the default and follows your device's appearance, including live changes while the page is open. Your choice is saved in the browser for the local server.
- The **Dark** theme pairs charcoal surfaces with restrained honey-bronze accents; lane filters, card IDs, and tags stay neutral so primary actions stand out.
- The **Light** theme uses teal accents.
- Code highlighting, Mermaid diagrams, and cycle-time reports follow the selected theme.
- Scrollbars use slim, rounded neutral styling in both themes; system high-contrast mode keeps native scrollbars.
