# 29 - Support multiple items in commit command

## Description

Allow `markban commit` to accept multiple item IDs or slugs in a single invocation. Currently each call does its own `git add . / git commit / git push`, so committing N items requires N commits. Multi-item support should move all specified items to Done and then perform a single `git add / git commit / git push`.

Syntax proposal:
```
markban commit 15,16,17 --tag feat --message "multiboard support"
markban commit 15 16 17 --tag feat --message "multiboard support"
```

Changes required:
- `CommitRoute`: detect comma-separated or space-separated IDs after `commit`, collect into a list
- `CommitCommand.ExecuteAsync`: accept `IReadOnlyList<string>` identifiers; loop moves-to-Done, then single git operations at the end
- Update help text
- Update `CommandRouterTests` route count if needed
- Add unit/integration tests for multi-item path

---

## Acceptance Criteria

- [ ] `markban commit 15,16,17 --tag feat --message "..."` moves all three items to Done and produces one git commit
- [ ] `markban commit 15 16 17 --tag ...` (space-separated) also works
- [ ] Single-item usage unchanged — no behaviour regression
- [ ] `--dry-run` lists all items that would be moved and the single commit that would be made
- [ ] Unknown item ID in the list aborts with a clear error before any moves or git operations
- [ ] Help text updated to show new syntax
