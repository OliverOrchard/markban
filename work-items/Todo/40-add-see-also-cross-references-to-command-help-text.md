# 40 - Add see-also cross-references to command help text

## Description

`HelpEntry.Detail` is already rendered when a user runs `markban help <command>`. Populate the `Detail` field on every relevant route with a "See also:" block pointing to related commands. This means a user (or agent) who looks up one command will naturally discover the others they need -- without needing to know the full command list upfront.

The format should be consistent across all commands:

```
See also:
  markban <cmd>    <one-line description>
```

Depends on [lane-rename-command], [lane-add-command], [lane-remove-command], [board-add-command], [board-remove-command] being implemented first so their see-also entries are accurate.

---

## Acceptance Criteria

### Item lifecycle cluster
- [ ] `create` -- See also: `next`, `reorder`, `lane add`
- [ ] `move` -- See also: `progress`, `commit`
- [ ] `progress` -- See also: `move`, `commit`
- [ ] `commit` -- See also: `move`, `progress`, `git-history`
- [ ] `rename` -- See also: `references`, `health check-links`

### Discovery cluster
- [ ] `list` -- See also: `overview`, `search`, `next`
- [ ] `next` -- See also: `list`, `progress`
- [ ] `show` -- See also: `references`, `rename`
- [ ] `search` -- See also: `show`, `references`
- [ ] `next-id` -- See also: `create`

### Health & maintenance cluster
- [ ] `health` -- See also: `sanitize`, `references`
- [ ] `sanitize` -- See also: `health`, `rename`
- [ ] `references` -- See also: `rename`, `health check-links`
- [ ] `git-history` -- See also: `commit`

### Board setup cluster
- [ ] `init` -- See also: `board add`, `lane add`
- [ ] `web` -- See also: `board add`, `board remove`, `overview`
- [ ] `overview` -- See also: `web`, `board add`, `list`

### Lane & board management cluster
- [ ] `lane rename` -- See also: `lane add`, `lane remove`
- [ ] `lane add` -- See also: `lane rename`, `lane remove`, `init`
- [ ] `lane remove` -- See also: `lane rename`, `lane add`, `health`
- [ ] `board add` -- See also: `init`, `board remove`, `overview`, `web`
- [ ] `board remove` -- See also: `board add`, `overview`

### Format & consistency
- [ ] All `See also:` blocks use the same format: two-space indent, command left-aligned, tab-separated one-line description
- [ ] Commands that already have no meaningful related commands (e.g. `help`) are left unchanged
- [ ] `markban help <cmd>` renders the see-also block for every command listed above
- [ ] Unit test: `HelpEntry.Detail` for each updated route contains the expected `See also:` text
