# Documentation compilation fixtures

Run `npm --prefix docs run check:samples` from the repository root. Source is
extracted from Markdown, not copied here. See
[contributing](../docs/contributing.md#compiling-documentation-examples) for the
classification convention, local prerequisites and how to add examples.

Projects cover the README, complete first-test walkthrough, native composition
and inheritance for all four adapters, assembly configuration, HTML exports,
upload API, report hosting/options migration and independent report contracts.
They also cover standalone execution through snapshot/JSON/HTML export and both
persistent storage registrations. `Standalone` is an executable smoke example;
`Storage` only compiles alternative registrations and does not contact Azure.
The [consumer API review](../api-reviews/1017-consumer-api.md) records the reviewed
entry points, ownership rules, evidence and finding dispositions.
`Support.cs` files supply only the application models omitted by the adapter
pages. They do not mock library or framework APIs. The separate `Readme` and `FirstTest`
projects support the local smoke-test commands; the other projects compile
alternative configurations and report hooks that should not all run together.

## README consolidation map

The old README's detailed reference remains in these maintained pages. New
feature guidance belongs in those pages; keep the README as the short entry point.

| Former README material | Documentation |
| --- | --- |
| Purpose, SpecFlow comparison, installation, packages and targets | [Introduction](../docs/intro.md), [installation](../docs/getting-started/installation.md), [SpecFlow](../docs/migration/from-specflow.md) |
| Complete account example and reports | [First test](../docs/getting-started/first-test.md), [NUnit](../docs/frameworks/nunit.md), [HTML reports](../docs/reports/html-report.md) |
| Assembly ownership, identity, async setup and explicit runs | [Assembly runs](../docs/configuration/assembly-runs.md), [context](../docs/writing-tests/test-context.md), [explicit runs](../docs/advanced/explicit-runs.md), [adapters](../docs/frameworks/overview.md) |
| Delegates, xUnit v3, status and JSON migration | [Major-version entry point](../docs/migration/next-major.md) and its focused guides |
| Converters, step text, arguments, tags and information | [Step text](../docs/writing-tests/step-text.md), [tags](../docs/writing-tests/tags-and-information.md) |
| Single execution, But steps, outlines, async steps and synchronization | [Scenarios](../docs/writing-tests/scenarios.md), [outlines](../docs/writing-tests/scenario-outlines.md), [async steps](../docs/writing-tests/async-steps.md) |
| Step hooks, retry history and retry-hook failures | [Step hooks](../docs/writing-tests/step-hooks.md), [retries](../docs/writing-tests/retries.md), [retry errors](../docs/configuration/retry-hook-errors.md) |
| Skips, exception outcomes and timeouts | [Skipping](../docs/writing-tests/skipping.md), [exceptions](../docs/configuration/exceptions.md), [timeouts](../docs/writing-tests/timeouts.md) |
| Uploads, proxies, authorization, sample host and screenshots | [Sending results](../docs/report-server/sending-results.md), [hosting](../docs/report-server/hosting.md), [security](../docs/report-server/security.md), [sample server](../docs/report-server/sample-server.md), [overview](../docs/report-server/overview.md) |
| Cache capacity, retention, comparisons, persistence and deletion | [Caching](../docs/report-server/caching-and-limits.md), [persistence](../docs/report-server/persistence.md), [storage migration](../docs/migration/report-storage.md) |
| Date formatting and custom HTML | [Customization](../docs/report-server/customization.md), [output](../docs/writing-tests/output.md), [HTML security](../docs/reports/html-report-security.md) |
| Repository tests and package policy | [Contributing](../docs/contributing.md), [BUILDING.md](../../BUILDING.md) |
