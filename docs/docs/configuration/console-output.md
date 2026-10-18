---
title: Console output
description: Plain-text reporting and output capture during parallel scenarios.
---

# Console output

BDTest writes scenario headers, exception details, retry notices, separators, and
summaries as plain text. The reporter does not change `Console.ForegroundColor`,
`Console.BackgroundColor`, or reset console colours. This applies to both captured
output and messages written only to the original console, so parallel scenarios
do not change the colours used by other tests or the test runner.

Console output from a step is captured for that step and included in its scenario's
report. Captured text keeps its original content and order within each scenario.
Different scenarios can still interleave their messages on the shared console;
their captured output remains separate. Console-only separators and summaries are
not added to the captured step output.
