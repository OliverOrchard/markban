# 26 - Cycle time reporting in web UI

## Description

Add a report page to the web UI showing cycle time data derived from git history. Cycle time = time from when an item first appeared in the `inProgress` lane to when it appeared in the `terminal` lane. Both timestamps are available from git log on the respective files.

**Report content:**
- Scatter plot: each completed item as a dot, x-axis = completion date, y-axis = cycle time in days
- Percentile lines: 50th, 85th, 95th overlaid on the scatter plot
- Summary: avg, median, 85th percentile cycle time
- Filterable by date range and tags

**Data source:** `GitHistoryCommand` logic already extracts file move events from git log. Cycle time computation reads those events, pairs `inProgress` entry with `terminal` entry per item, computes delta.

**Why the web UI?** A graph needs a canvas. The CLI can output a text summary (`--cycle-time` command) but the scatter plot lives in the web UI as a separate `/reports/cycle-time` route.

**CLI companion:** `markban --cycle-time` prints a text table of recent completed items with their cycle times and the percentile summary. No graph.

---

## Acceptance Criteria

- [ ] `/reports/cycle-time` route in web UI renders scatter plot with percentile lines
- [ ] Data derived from git history, no additional state storage needed
- [ ] Respects configured lane names from config -- no hardcoded `"Done"` or `"In Progress"` strings
- [ ] Filterable by date range
- [ ] `markban --cycle-time` CLI command prints text table summary
- [ ] Empty state handled gracefully (no completed items yet)
