# 38 - board add command

## Description

Add `markban board add <name> <path>` to create and/or register a new board. The command is a single atomic operation covering bootstrapping the target directory and optionally linking it into the current board's `boards` array.

Depends on [fix-root-board-always-visible-when-boards-array-is-configured] (so the root board is not lost when the `boards` array is first written).

Usage:
```
markban board add "Mobile" ../mobile-project            # bootstrap + link
markban board add "Mobile" ../mobile-project --no-link  # bootstrap only, standalone
markban board add "Mobile" ../mobile-project --copy-config  # clone board-local settings
```

---

## Acceptance Criteria

- [x] `markban board add <name> <path>` creates the directory at `<path>` if it does not exist, then runs `init` logic (creates lane directories and `markban.json`) and appends a `boards` entry in the current root config
- [x] If `<path>` exists and is already a valid board, the directory is not modified -- only the `boards` entry is added
- [x] `<path>` is resolved relative to the current root config file location (not the CWD), keeping the config portable
- [x] `--no-link` bootstraps the board at `<path>` but does NOT modify the current root config -- useful for creating a standalone board
- [x] `--copy-config` copies board-local settings (`lanes`, `wipLimits`, `commitTags`, `commitMessageMaxLength`, `slugCasing`, `h1Heading`) from the current board into the new board's `markban.json`. Does NOT copy `rootPath` or `boards`
- [x] When the `boards` array is first written, the root board is included as the first entry (see [fix-root-board-always-visible-when-boards-array-is-configured])
- [x] If a board with the same key already exists in the `boards` array, an error is printed and nothing is changed
- [x] `--dry-run` prints what would be created/modified without making changes
- [x] Unit test: new board directory and config created; entry appended to root config
- [x] Unit test: existing board directory reused without modification
- [x] Unit test: `--no-link` does not modify root config
- [x] Unit test: `--copy-config` copies only board-local settings
- [x] Unit test: root board preserved in `boards` array on first write
- [x] Unit test: error on duplicate board key
