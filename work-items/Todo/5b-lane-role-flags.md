# 5b - Lane role flags

## Description

Wire the `type` and `pickable` lane config properties into the commands that currently hardcode `"Done"` and `"Todo"`. Depends on [update-commands-and-tests-for-lanes] (lane names from config) being complete first.

**`type: "ready"`** -- the commitment lane. `--create` without `--lane` defaults here. `--next` returns the top-priority item from this lane. Only one lane should have this type.

**`type: "done"`** -- the terminal lane. `--commit` moves items here. Sorted descending by ID (most recently completed first). Validation should error if zero or multiple lanes have this type.

**`pickable: false`** -- explicitly excludes a lane from the pick chain. Applied to lanes like `Ideas` or `Rejected` that should never be the source for `--next` or the default target for `--create`. Defaults to `true` if omitted.

**Array order** drives the `--progress` command. `--progress <id>` advances an item to the next lane in config order, skipping lanes with `pickable: false`. Stops at the `done` lane. See [progress-command] for the dedicated implementation item.

**Failure modes to handle:**
- No `type: "done"` lane configured -> clear error on `--commit`: "No done lane configured in markban.json"
- Multiple `type: "done"` lanes -> error at config load time
- No `type: "ready"` lane -> error on `--next` and `--create` (without explicit `--lane`) with pointer to config
- `--progress` at the `done` lane -> informational message, no move performed

---

## Acceptance Criteria

- [ ] `--commit` resolves terminal lane via `type: "done"`, not hardcoded `"Done"`
- [ ] `--create` default lane resolved via `type: "ready"`, not hardcoded `"Todo"`
- [ ] `--next` reads from `type: "ready"` lane, not hardcoded `"Todo"`
- [ ] `--progress <id>` advances item one lane forward in config array order
- [ ] `--progress` skips lanes with `pickable: false`
- [ ] `--progress` stops and informs user when item is already in the `done` lane
- [ ] `pickable: false` lanes excluded from `--next` source and `--create` default
- [ ] Config validation errors clearly when `type: "done"` is missing or duplicated
- [ ] Config validation errors clearly when `type: "ready"` is missing on `--next` or `--create`
- [ ] All existing tests still pass with default config (which sets these types on standard lanes)
- [ ] New unit tests covering custom lane type assignments
