# 5a - Update commands and tests for configurable lanes

## Description

Once `markban.json` lane config is in place (parent [configurable-lanes]), every command that references a lane name needs to read from config rather than a hardcoded array. This is the sweep to make all of that work and to update integration and unit tests accordingly.

**Commands requiring changes:**

| Command | Change needed |
|---|---|
| `WorkItemStore.LoadAll` | Iterate configured lanes, not hardcoded folder list |
| `MoveCommand` | Validate target against configured lanes; handle ordered/unordered rename logic per lane type |
| `CreateCommand` | Validate `--lane` against configured lanes; create in correct subdirectory |
| `ReorderCommand` | Only apply to lanes where `ordered: true`; reject unordered lanes with a clear error |
| `NextIdCommand` | Only count items in ordered lanes when computing next ID |
| `OverviewCommand` | Display only configured lanes; group ordered vs unordered correctly |
| `ListCommand` | `--folder` filter validates against configured lane names |
| `CommitCommand` | Moves to "done" lane -- needs to know which lane is the terminal/done lane |
| `HelpCommand` | Valid lane names in help text should come from config, not hardcoded |
| `WebServer` | Board columns reflect configured lanes in order |
| `CheckLinksCommand` | Lane iteration should use config |
| `SanitizeCommand` | Lane iteration should use config |

**Testing:**
- Integration tests currently assert against hardcoded lane names (`Todo`, `In Progress`, etc.) -- these need a config-aware test workspace setup
- Unit tests for `MoveCommand`, `CreateCommand`, `ReorderCommand` need test cases for custom lane configs
- Add integration test: board with non-default lanes (e.g. `Doing`, `Blocked`) works end-to-end

---

## Acceptance Criteria

- [ ] All commands listed above read lane list from config (or defaults if not configured)
- [ ] No hardcoded lane name strings remain outside of the default config definition
- [ ] `--move` rejects a lane not in config with a clear error listing valid lanes
- [ ] `--reorder` rejects an unordered lane with a clear error
- [ ] Integration tests updated -- `TestWorkspace` can be configured with custom lanes
- [ ] New integration test covers a board with fully custom lane names
- [ ] Help text for `--move` and `--create --lane` reflects configured lanes at runtime
