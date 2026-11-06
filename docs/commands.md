# CLI Commands and File Format

## How it works

Work items are `.md` files inside a `work-items/` directory at your project root (or the directory configured by `rootPath` in [`markban.json`](configuration.md)):

```text
work-items/
  Todo/
    1-implement-login.md
    2-add-tests.md
  In Progress/
    3-redesign-header.md
  Testing/
  Done/
  Ideas/
  Rejected/
```

**Number = priority.** In ordered lanes (`Todo`, `In Progress`, `Testing`, `Done`), lower numbers have higher priority (`1` is highest). Renaming a file changes its priority -- no database, no external state, just filenames. Unordered lanes (`Ideas`, `Rejected`) store slug-only filenames without a number prefix.

**Sub-items** use letter suffixes (`12a-`, `12b-`, `12c-`) to group related tasks under a parent item.

**Cross-references** use `[slug]` syntax between items (for example, `[redesign-header]`). Legacy `WI-N` references are also recognized, and `markban sanitize` converts `WI-N` references to `[slug]` form.

**Frontmatter** (YAML between `---` markers at the top of a work item) stores optional metadata managed by the CLI: `blocked`, `tags`, `dependsOn`, and any configured `customFrontmatter` fields.

## Quick workflow

```bash
markban init
markban create "Add board switcher"
markban next
markban move 1 "In Progress"
markban show 1
markban web
markban progress 1
markban commit 1 --tag feat --message "add board switcher" --dry-run
```

## Command overview

Run `markban` from anywhere inside your project tree; it walks up parent directories to locate `markban.json` or `work-items/`.

```text
markban web [--port <port>] [--no-open]
markban help [command]
markban init [--path <dir>] [--name "Board Name"] [--dry-run]
markban list [--folder <lane>] [--summary] [--filter-tag <tag>]
markban next [--include-blocked]
markban show <id|slug>
markban search <term> [--full]
markban move <id|slug> <lane> [--override-wip]
markban progress <id|slug> [--dry-run]
markban next-id
markban reorder <lane> <order> [--no-sub-items] [--dry-run] [--start-number <n>] [--yes]
markban create "Title" [--lane <lane>] [--after <id>] [--priority] [--override-wip] [--tags <t1,t2>] [--set key=value]
markban create "Title" --sub-item --parent <id> [--lane <lane>] [--after <id>] [--override-wip]
markban rename <id|slug> "New Title" [--dry-run]
markban overview
markban sanitize
markban health [check-links|check-order] [--include-ideas] [--fix]
markban references <slug|id> [--include-ideas]
markban git-history <file>
markban commit <id[,id2...]> --tag <tag> --message "msg" [--dry-run]
markban block <id|slug> ["reason"] | --remove | --list
markban tag <id|slug> [<tags>] [--remove <tag>]
markban depends-on <id|slug> [<dep-slug>] [--remove <slug>] [--list]
markban cycle-time
markban start <id> [--no-pull] [--dry-run]
markban board remove <key> [--dry-run]
markban board add <name> <path> [--no-link] [--copy-config] [--dry-run]
markban lane rename <old-name> <new-name> [--dry-run]
markban lane add <name> [--unordered] [--type <ready|done>] [--not-pickable] [--dry-run]
markban undo [--dry-run] [--list]
markban redo [--dry-run] [--list]
```

**Global flags:**

- `--root <path>` -- Override the `work-items` directory location for any command.

---

## Command reference

### Board setup and server

#### `init`

```text
markban init [--path <dir>] [--name "Board Name"] [--dry-run]
```

Scaffold a new board in the current directory. Writes `markban.json` with explicit defaults and creates all configured lane directories. If `markban.json` already exists, `init` reads it and creates any missing lane directories without overwriting the file.

- `--path <dir>` -- Use a custom board directory instead of `work-items/`.
- `--name "name"` -- Set a display name in `markban.json`.
- `--dry-run` -- Preview the config and directories that would be created without touching the filesystem.

#### `web`

```text
markban web [--port <port>] [--no-open]
```

Start the local web board UI (default port: `5000`). See [Web UI](web-ui.md).

- `--port <port>` -- Server port (default: `5000`).
- `--no-open` -- Do not open the browser automatically.

#### `help`

```text
markban help [command]
```

Show general CLI help, or detailed flag help for a specific command (also available via `markban <command> --help` or `-h`). Commands whose feature is disabled in `markban.json` (`block`, `tag`, `depends-on`) are hidden from the default `help` listing.

---

### Querying items

#### `list`

```text
markban list [--folder <lane>] [--summary] [--filter-tag <tag>]
```

List work items as JSON (default: all lanes, full item objects including Markdown content).

- `--folder <lane>`, `-f <lane>` -- Filter to a specific lane (e.g. `Todo`, `"In Progress"`, `Testing`, `Done`, `Ideas`, `Rejected`).
- `--summary`, `-s` -- Compact JSON output (`id`, `slug`, and `status` only).
- `--filter-tag <tag>` -- Show only items with this tag (comma-separated for multiple tags; shown in help when `tags.enabled` is `true`).

#### `next`

```text
markban next [--include-blocked]
```

Return the highest-priority pickable `ready`-lane (`Todo`) item as JSON. Skips items marked `blocked` or with unresolved `dependsOn` references by default.

- `--include-blocked` -- Include blocked and dependency-blocked items.

#### `show`

```text
markban show <id|slug>
```

Print a single work item as JSON by its numeric/sub-item ID (e.g. `1`, `12a`) or slug.

#### `search`

```text
markban search <term> [--full]
```

Ranked search across work item slugs, IDs, and titles.

- `<term>` -- Search term.
- `--full` -- Also scan Markdown body content.

#### `next-id`

```text
markban next-id
```

Print the next safe work item number (`max + 1` across all ordered lanes).

#### `overview`

```text
markban overview
```

Print a compact project progress summary (item counts per lane and completion percentage). When `boards` is configured in `markban.json`, prints progress for each board.

---

### Creating and editing items

#### `create`

```text
markban create "Title" [--lane <lane>] [--after <id>] [--priority] [--override-wip] [--tags <t1,t2>] [--set key=value]
markban create "Title" --sub-item --parent <id> [--lane <lane>] [--after <id>] [--override-wip]
```

Create a new work item using the default structure or [`work-items/.template.md`](item-template.md).

- `"Title"` -- Required work item title.
- `--lane <lane>` -- Target lane (default: the configured `ready` lane, `Todo`).
- `--after <id>` -- Insert immediately after this item ID and renumber subsequent items in the lane.
- `--priority` -- Insert at the top of the lane (`1`) and renumber subsequent items.
- `--sub-item` -- Create as a lettered sub-item (`<parent>a`, `<parent>b`, ...); requires `--parent <id>`.
- `--parent <id>` -- Parent item ID when using `--sub-item`.
- `--override-wip` -- Bypass the WIP limit for the target lane.
- `--tags <t1,t2>` -- Comma-separated tags to apply on creation (when `tags.enabled` is `true`).
- `--set key=value` -- Set a configured `customFrontmatter` field (repeatable).

#### `rename`

```text
markban rename <id|slug> "New Title" [--dry-run]
```

Rename a work item: updates the `# {id} - Title` H1 heading, regenerates the filename slug using `slugs.casing`, and updates `[old-slug]` cross-references and `dependsOn` entries across the board.

- `<id|slug>` -- Item to rename.
- `"New Title"` -- New title.
- `--dry-run` -- Preview file and reference changes without writing.

---

### Moving, reordering, and workflow

#### `move`

```text
markban move <id|slug> <lane> [--override-wip]
```

Move an item to `<lane>`. Moving to an unordered lane (`Ideas`, `Rejected`) strips the numeric prefix; moving from an unordered lane into an ordered lane assigns the next available ID. Enforces lane `wip` limits unless `--override-wip` is passed.

- `<id|slug>` -- Item to move.
- `<lane>` -- Target lane name.
- `--override-wip` -- Bypass the WIP limit for the target lane.

#### `progress`

```text
markban progress <id|slug> [--dry-run]
```

Advance an item one lane forward in the workflow order defined by the `lanes` array in `markban.json` (excluding `pickable: false` holding lanes).

- `<id|slug>` -- Item to advance.
- `--dry-run` -- Preview the target lane without moving the file.

#### `start`

```text
markban start <id> [--no-pull] [--dry-run]
```

Start work on an item: moves it from the `ready` lane (`Todo`) to the next active lane (`In Progress`). When `git.featureBranches.enabled` is `true`, checks out `mainBranch`, pulls (unless `--no-pull` is passed or `pullOnStart` is `false`), and creates/checks out `<branchPrefix><id>-<slug>`.

- `<id>` -- Work item ID or slug to start.
- `--no-pull` -- Skip `git pull` before branching (feature-branch mode only).
- `--dry-run` -- Show what would happen without making changes.

#### `reorder`

```text
markban reorder <lane> <order> [--no-sub-items] [--dry-run] [--start-number <n>] [--yes]
```

Reorder items within an ordered lane by specifying a comma-separated list of IDs (highest priority first). Items omitted from `<order>` are placed after the listed items in their existing relative order.

- `<lane>` -- Target lane (e.g. `Todo`, `"In Progress"`).
- `<order>` -- Comma-separated IDs, highest priority first; lettered sub-items (e.g. `32a`) are detached per the `subItemReorder` setting in `markban.json` (`"warn"`, `"block"`, or `"force"`).
- `--no-sub-items` -- Exclude sub-items from reorder.
- `--dry-run` -- Preview the rename plan without executing.
- `--start-number <n>` -- Assign the first reordered item this positive integer instead of keeping the lane's current lowest number.
- `--yes` -- Skip the `Continue? [y/N]` prompt when `subItemReorder` is `"warn"`.

#### `commit`

```text
markban commit <id[,id2...]> --tag <tag> --message "msg" [--dry-run]
```

Move one or more items to the `done` lane (`Done`), then run `git add .`, `git commit -m "<tag>: <msg>"`, and `git push` (when `git.enabled` is `true`). Honors `commit.maxMessageLength`, `commit.tags`, and `git.featureBranches` (`single`, `multiple`, or `squash` strategy, `prCommand`, and `checkoutOnDone`).

- `<id[,id2...]>` -- One or more item IDs or slugs (comma- or space-separated).
- `--tag <tag>` -- Conventional-commit tag. Default valid tags: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `build`, `ci`, `chore`, `revert`, `perf`.
- `--message "msg"` -- Single-sentence commit description (default max length: 72 characters).
- `--dry-run` -- Preview lane moves and git commands without executing.

---

### Frontmatter metadata (`block`, `tag`, `depends-on`)

#### `block`

```text
markban block <id|slug> "reason"
markban block <id|slug> --remove
markban block --list
```

Mark a work item as blocked (sets `blocked: "reason"` in YAML frontmatter), unblock it (`--remove`), or list all blocked items across lanes (`--list`). Hidden when `blocked.enabled` is `false`.

#### `tag`

```text
markban tag <id|slug> [<tags>] [--remove <tag>]
```

Add, remove, or list tags on a work item (stored in `tags` frontmatter). Hidden when `tags.enabled` is `false`.

- `markban tag <id> bug,backend` -- Add comma-separated tags (idempotent).
- `markban tag <id> --remove bug` -- Remove a tag.
- `markban tag <id>` -- List current tags for the item.

#### `depends-on`

```text
markban depends-on <id|slug> [<dep-slug>] [--remove <slug>] [--list]
```

Manage or query item dependencies stored in `dependsOn` frontmatter. Hidden when `dependsOn.enabled` is `false`.

- `markban depends-on <id> <slug>` -- Add a dependency by slug or ID (resolved and stored as slug; idempotent).
- `markban depends-on <id> --remove <slug>` -- Remove a dependency.
- `markban depends-on <id>` -- List dependencies for `<id>` with their current lane/status.
- `markban depends-on --list` -- List all items with unresolved dependencies (dependencies not yet in `Done`).

---

### Board hygiene and history

#### `health`

```text
markban health [check-links|check-order] [--include-ideas] [--fix]
```

Run board diagnostics. Running `markban health` with no subcommand runs all checks and exits non-zero if any issue is found.

- `check-links` -- Check for broken `[slug]` cross-references, bare numeric `WI-N` references, and broken `dependsOn` targets.
- `check-links --include-ideas` -- Also scan `Ideas` and `Rejected` lanes.
- `check-order` -- Check numeric ordering in ordered lanes (duplicates, gaps, and out-of-order `dependsOn` items within the same lane).
- `check-order --fix` -- Auto-fix numeric ordering issues where safe.

#### `references`

```text
markban references <slug|id> [--include-ideas]
```

List all work items that reference `[slug]`.

- `<slug|id>` -- Target item slug or ID.
- `--include-ideas` -- Also search `Ideas` and `Rejected` lanes.

#### `sanitize`

```text
markban sanitize
```

Normalize Unicode punctuation/whitespace in work-item files and convert `WI-NNN` references to `[slug]` links.

#### `git-history`

```text
markban git-history <file>
```

Show work-item activity extracted from the git commit and rename history of `<file>`.

#### `cycle-time`

```text
markban cycle-time
```

Report cycle time (time from first active lane entry to `Done`) for completed work items using git history.

---

### Lane and board management

#### `lane rename`

```text
markban lane rename <old-name> <new-name> [--dry-run]
```

Atomically rename a lane directory on disk and update its `name` in `markban.json`.

- `<old-name>` -- Existing configured lane name.
- `<new-name>` -- New lane name.
- `--dry-run` -- Preview without executing.

#### `lane add`

```text
markban lane add <name> [--unordered] [--type <ready|done>] [--not-pickable] [--dry-run]
```

Add a lane to `markban.json` and create its directory.

- `<name>` -- Required new lane name.
- `--unordered` -- Lane is not priority-ordered (default: ordered).
- `--type <ready|done>` -- Mark as the `ready` or `done` lane (only one of each role is allowed per board).
- `--not-pickable` -- Exclude the lane from `markban next` and `create` defaults.
- `--dry-run` -- Preview without executing.

> **Planned (not yet available):** `markban lane remove` and `markban lane reorder` are planned for a future release and are not currently available. To remove or reorder lanes today, edit the `lanes` array in `markban.json` directly.

#### `board` (`add` and `remove`)

```text
markban board remove <key> [--dry-run]
markban board add <name> <path> [--no-link] [--copy-config] [--dry-run]
```

Manage child boards in `markban.json`:

- `remove <key> [--dry-run]` -- Remove the board with `<key>` from the `boards` array in `markban.json`. Its directory on disk is left untouched. If `boards` becomes empty, the `boards` key is removed (returning to single-board mode). The root board cannot be removed from its own config.
- `add <name> <path> [--no-link] [--copy-config] [--dry-run]` -- Bootstrap a board directory at `<path>` (or reuse an existing board directory) and register it in the root `markban.json`:
  - `--no-link` -- Bootstrap the board directory without modifying the current root `markban.json`.
  - `--copy-config` -- Copy board-local settings from the current board when bootstrapping.
  - `--dry-run` -- Preview without modifying the filesystem.

---

### Undo and redo

```text
markban undo [--dry-run] [--list]
markban redo [--dry-run] [--list]
```

Undo or re-apply the most recent non-dry-run `reorder` or `move` operation using the persisted history journal in `.markban/history.json` (see [`history` in `markban.json`](configuration.md#history-properties)). Before restoring files, markban verifies that every affected file still matches its recorded SHA-256 content hash; if a file was modified, moved, or deleted outside the journal, `undo`/`redo` refuses and leaves the filesystem unchanged.

- `--dry-run` -- Preview which files would be restored or moved without writing.
- `--list` -- Show the undo and redo history stacks.
