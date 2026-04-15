# Migrate config to TOML

## Description

Replace `markban.json` with `markban.toml` as the project config format. TOML is designed for human-maintained config files, supports comments, has cleaner syntax for nested structures, and is the direction modern CLI tools are moving (`Cargo.toml`, `pyproject.toml`, etc.).

This is a **breaking change** for anyone already using `markban.json`. Migration path should include:
- `FindRoot` supports both files during a transition period, preferring `markban.toml` if both exist
- A `markban migrate` subcommand (or auto-migration on `init`) that converts `markban.json` -> `markban.toml` and deletes the old file
- Clear deprecation warning when `markban.json` is detected

**Implementation:** `Tomlyn` is the standard .NET TOML library (single NuGet package, well maintained).

Example config in TOML:

```toml
# Path to the board directory (default: work-items)
root_path = "./work-items"

[[boards]]
name = "Backend"
path = "services/api"

[[boards]]
name = "Frontend"
path = "services/web"

[git]
enabled = true

[web]
port = 5000
```

Park until the `boards` feature and `init` command are stable -- the config surface will be clearer by then and worth migrating all at once.

## Notes

- Do not do this before v1.0 -- JSON is widely understood and the current config is small enough that comments aren't missed yet
- Consider supporting both formats permanently (TOML preferred, JSON fallback) to avoid a hard break
