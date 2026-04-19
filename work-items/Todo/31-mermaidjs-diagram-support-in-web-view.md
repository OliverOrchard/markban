# 31 - MermaidJS diagram support in web view

## Description

Work item markdown files may contain Mermaid diagram definitions in fenced code blocks (` ```mermaid `). The web view should detect these blocks and render them as interactive SVG diagrams using the Mermaid.js library.

Depends on [rich-markdown-rendering-in-web-view] (markdown rendering pipeline must be in place first).

---

## Acceptance Criteria

- [ ] Fenced code blocks tagged ` ```mermaid ` are rendered as Mermaid diagrams (SVG) in the web view
- [ ] Mermaid.js is loaded from CDN (documented in code); no npm build pipeline is introduced
- [ ] Supported diagram types include at minimum: flowchart, sequence diagram, gantt, class diagram
- [ ] If a Mermaid block contains a syntax error, an error message is shown inline rather than crashing the page
- [ ] Non-mermaid code blocks are unaffected and still receive syntax highlighting (see [rich-markdown-rendering-in-web-view])
- [ ] Diagrams render correctly in both light and dark browser themes
- [ ] Playwright UI test: a work item with a ` ```mermaid ` block renders an `<svg>` element on the page
- [ ] Playwright UI test: a work item with an invalid Mermaid block shows an error element rather than a blank or crashed page
- [ ] Playwright UI test: a non-mermaid fenced code block on the same page still renders as highlighted `<pre><code>` (regression guard)
- [ ] Existing integration tests for the web UI still pass
