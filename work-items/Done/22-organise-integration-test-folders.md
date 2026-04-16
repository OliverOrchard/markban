# 22 - Organise integration test folders

## Description

Reorganise `Markban.IntegrationTests` so the suite is easier to navigate as it grows. Keep a single integration test project for now, but separate tests by area so CLI coverage, web API coverage, web UI coverage, and shared test infrastructure are not all mixed together at the project root.

Chosen structure:
- `Cli/` for command-oriented end-to-end tests
- `Web/Api/` for HTTP-level web integration tests
- `Web/Ui/` for browser smoke tests
- `Infrastructure/` for shared fixtures and helpers

This is a layout cleanup only. It should not change behaviour, dependencies, or how the test suite is executed.

---

## Acceptance Criteria

- [x] Integration tests are grouped into CLI, web API, web UI, and shared infrastructure folders
- [x] The reorganisation keeps a single `Markban.IntegrationTests` project
- [x] The test suite still passes after the folder cleanup
