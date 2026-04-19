# 30 - Rich markdown rendering in web view

## Description

The web view currently renders markdown with minimal styling. Upgrade the markdown rendering pipeline so that work item content is fully and richly rendered in the web UI, matching the expressiveness of the markdown format.

---

## Acceptance Criteria

- [ ] A proper markdown-to-HTML library is used (e.g. marked.js or markdown-it via CDN) instead of any hand-rolled rendering
- [ ] Code blocks (fenced with triple backticks) are rendered with syntax highlighting per language (e.g. via highlight.js or Prism.js)
- [ ] Tables render correctly with borders/styling
- [ ] Blockquotes, horizontal rules, inline code, bold, italic, strikethrough all render correctly
- [ ] Ordered and unordered lists (including nested) render correctly
- [ ] Task list checkboxes (`- [ ]` / `- [x]`) render as visual checkboxes (read-only in the view)
- [ ] Headings (H1-H6) render with appropriate visual hierarchy
- [ ] No external network requests are made at runtime -- libraries loaded from CDN are acceptable as a build-time decision but must be documented
- [ ] Playwright UI test: a work item containing a GFM table renders `<table>` elements visible on the page
- [ ] Playwright UI test: a fenced code block renders a `<pre><code>` element with a language-specific highlight class
- [ ] Playwright UI test: task list items render as `<input type="checkbox">` elements
- [ ] Existing integration tests for the web UI still pass
