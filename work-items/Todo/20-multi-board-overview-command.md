# 20 - Multi-board overview command

## Description

Extend `--overview` so that when a `boards` array is configured in `markban.json`, it prints a progress summary for every board in sequence, each headed by the board name. When no `boards` array is present, behaviour is identical to today.

Example output:

```
=== Backend ===
[##########....................] 33% -- 2 done, 1 testing, 3 active, 5 todo (10 total)

=== Frontend ===
[######################........] 73% -- 8 done, 0 testing, 1 active, 2 todo (11 total)
```

This is purely additive -- no new flags needed, the config drives it.

---

## Acceptance Criteria

- [ ] Single board config: output identical to current behaviour
- [ ] Multi board config: each board printed with its name as a heading
- [ ] Unreachable board path prints a warning and continues to next board
- [ ] Exit code 0 in all cases (overview is informational)
