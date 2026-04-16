# Web UI WIP limit visual indicator

## Description

Split from [configurable-wip-limits-per-lane]. The CLI enforcement of WIP limits is complete and done. This item covers the web UI side: column headers in `wwwroot/app.js` / `styles.css` should visually reflect whether a lane is at or near its configured `wip` limit.

Suggested behaviour:
- Column header turns **amber** when the lane is at >= 80% of its limit
- Column header turns **red** when the lane is at 100% (at or over limit)
- No visual change when no `wip` is configured for the lane

The lane `wip` value is already available in the config served to the web UI.

---

## Acceptance Criteria

- [ ] Web UI column header shows amber indicator when lane is >= 80% of its WIP limit
- [ ] Web UI column header shows red indicator when lane is at 100% of its WIP limit
- [ ] No visual change when lane has no `wip` set
- [ ] Indicator updates dynamically when items are moved via the web UI
