---
tags: [release]
---

# Publish to winget

## Description

Make markban installable on Windows with `winget install markban`. The release workflow only builds osx and linux binaries today, so this needs a Windows build and a winget manifest.

- Add a `win-x64` (optionally `win-arm64`) job to `.github/workflows/release.yml` that publishes a self-contained binary and uploads a `.zip` to the GitHub Release.
- Submit the first manifest manually to `microsoft/winget-pkgs` (reviewed by the winget team).
- Automate later versions with `wingetcreate` or the `winget-releaser` action, using a PAT that can open PRs from a fork of `winget-pkgs` (stored as a repo secret, like `HOMEBREW_TAP_TOKEN`).
- NuGet is intentionally not a distribution route.

---

## Acceptance Criteria

- [ ] Release workflow builds and uploads a Windows archive for each tag
- [ ] First manifest is accepted into `microsoft/winget-pkgs`
- [ ] `winget install markban` installs a working `markban` command
- [ ] A new tag opens the winget update PR automatically
- [ ] README and `docs/` mention the winget install command
