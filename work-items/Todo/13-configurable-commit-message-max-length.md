# 13 - Configurable commit message max length

# 14 - Configurable commit message max length

## Description

The commit message character limit is currently hardcoded at 72 characters (`MaxMessageChars` in `CommitCommand.cs`). This is a well-established git convention but not universal -- some teams use 50, some use 100. Make it configurable via `markban.json`.

```json
{
  "commit": {
    "maxMessageLength": 72
  }
}
```

Default remains 72 -- no change to existing behaviour. `markban init` writes this value explicitly so users can see and change it.

---

## Acceptance Criteria

- [ ] `commit.maxMessageLength` in config overrides the 72-char limit
- [ ] Default of 72 preserves current behaviour
- [ ] Error message on violation shows both the configured limit and the actual length
- [ ] `markban init` writes `commit.maxMessageLength: 72` in explicit defaults (see [explicit-defaults-in-init])
- [ ] `--dry-run` validation uses the configured limit
