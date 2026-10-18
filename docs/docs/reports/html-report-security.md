---
title: HTML report security
description: Custom HTML isolation and Content Security Policy in standalone reports.
---

# HTML report security

`BDTestHtmlHelper.GetTestHtmlReport()` returns a single offline HTML document with
embedded styles and JavaScript. Its Content Security Policy permits only the exact
report script, using a SHA-256 hash. Inline event handlers, other scripts, network
connections, plugins, forms, and base URL changes are blocked. Filtering, searching,
sorting, and expanding scenarios continue to work without external scripts.

`ScenarioHtmlWriter.Custom(html)` preserves raw HTML, but custom output appears in
an attribute-encoded `srcdoc` iframe. Scripts, event handlers, forms, top-level
navigation, and sandbox escape are disabled. Quotes, closing tags, and malformed
markup cannot escape into the report document or change other scenarios.

Formatting, inline styles, tables, and images remain available. The parent policy
allows inline styles and image sources because `srcdoc` also inherits that policy;
the frame's own policy continues to block scripts. Images can use data, HTTP, HTTPS,
or relative URLs. Remote images require network access.

Evidence links may open new tabs, which retain the sandbox restrictions. Reports
opened from disk retain the frame origin so relative screenshot files can load.
Keep those files beside the report at the relative paths written by the test.
Reports served over HTTP or HTTPS use an opaque frame origin.

Custom output remains test evidence, not trusted application code. It cannot add
JavaScript interactions to the report. Changing the embedded report script after
generation invalidates its CSP hash, so regenerate reports rather than editing
their script.
