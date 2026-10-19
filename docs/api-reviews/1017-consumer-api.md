# Consumer API review (#1017)

Reviewed against `ebb5c56` (2026-10-19), after the namespace/fluent-builder
migrations and the merge of context report adapters in #1021 (closing #1015).
This review follows the documented consumer path; it is not an assertion that
every public member has comprehensive XML documentation. No public signature,
target framework, package dependency or runtime behavior changes in this review.

## Entry points reviewed

| Consumer operation | Source and API evidence | Packages, imports and ownership |
| --- | --- | --- |
| Configure and own a standalone run | [`BDTestOptions`](../../BDTest/BDTestOptions.cs), [`BDTestRun.Start`, `GetSnapshot`, `Dispose`](../../BDTest/BDTestRun.cs) | `BDTest`; `using BDTest;`. Options are copied at start; hooks, callbacks, services and clocks remain shared references. The caller awaits work and exports before disposing the run. |
| Configure a framework run | [`BDTestAssemblyRuns`](../../BDTest/BDTestAssemblyRuns.cs); [`NUnitBDTestAssembly`](../../BDTest.NUnit/NUnitBDTestAssembly.cs), [`XUnitBDTestAssembly`](../../BDTest.xUnit/XUnitBDTestAssembly.cs), [`MSTestBDTestAssembly`](../../BDTest.MSTest/MSTestBDTestAssembly.cs), [`TUnitBDTestAssembly`](../../BDTest.TUnit/TUnitBDTestAssembly.cs) | Corresponding adapter package plus its test runner; authoring types use `BDTest`. Assembly configuration runs once on first lookup. Dispose through the adapter owner after assembly tests, teardown and exports; individual tests borrow the run. |
| Author and execute a scenario | [`BDTestBase`](../../BDTest/Test/BDTestBase.cs), [`BDTestBuilder`](../../BDTest/Test/BDTestBuilder.cs), [`RunnableStepBuilder.BDTestAsync`](../../BDTest/Test/RunnableStepBuilder.cs); [fluent migration](../docs/migration/fluent-builders.md) | `BDTest`, plus framework imports when using an adapter. A new builder has a distinct execution identity; one chain executes once. Async delegates have Task/ValueTask and cancellation-aware forms. `BDTestAsync(token)` returns `Task<ScenarioResult>`; direct await remains supported. |
| Read completed and pending results | [`BDTestUtil`](../../BDTest/BDTestUtil.cs), [`BDTestRunSnapshot`](../../BDTest/BDTestRunSnapshot.cs), [`ScenarioResult`](../../BDTest.Contracts/ScenarioResult.cs) | Core for live-run access; `BDTest.Contracts` for named immutable result types. Snapshot values outlive the run; captured attachment bytes do not. Snapshot again after teardown for later output. |
| Create and release per-attempt context | [`AbstractContextBDTestBase`](../../BDTest/Test/AbstractContextBDTestBase.cs), [`ContextCreation` and lifetime/factory interfaces](../../BDTest/Test/ContextCreation.cs), [`ServiceProviderContextFactory`](../../BDTest/Test/ServiceProviderContextFactory.cs) | `BDTest`, `using BDTest;`. Framework-owned contexts are recreated for retries and disposed with async disposal preferred when available. The DI factory constructs a fresh context; the caller still owns the provider, scope and injected dependencies. Existing adapter examples exercise typed contexts. |
| Export JSON and HTML | [`BDTestJsonHelper`](../../BDTest.Reporting/Helpers/BDTestJsonHelper.cs), [`BDTestHtmlHelper`](../../BDTest.Reporting/Helpers/BDTestHtmlHelper.cs) | `BDTest.Reporting`; `using BDTest; using BDTest.Helpers;`. Helpers require the selected live run and return strings; callers choose file I/O. Export after teardown and before disposal. |
| Render saved reports without execution | [`BDTestReportJson`](../../BDTest.Contracts/BDTestReportJson.cs), [`ReportHtmlRenderer`](../../BDTest.Rendering/ReportHtmlRenderer.cs); [rendering guide](../docs/reports/rendering.md) | `BDTest.Rendering` brings contracts, with `BDTest.Rendering` and `BDTest.Contracts` imports. Current-schema deserialization and rendering need no run, framework context or ASP.NET host. Historical input needs the documented legacy reader. |
| Map framework context metadata | [`BDTestReportContextExtensions`](../../BDTest.Reporting/Helpers/BDTestReportContextExtensions.cs); [context examples](../docs/reports/overview.md#framework-context-adapters) | `BDTest.Reporting`, `BDTest.Helpers`. Caller supplies a typed mapping while the context is valid and explicitly selects the run. Mapping does not retain context, infer scope or own disposal. |
| Upload results | [`BDTestReportUploader`](../../BDTest.Reporting/BDTestReportUploader.cs), [`BDTestReportUploadOptions`](../../BDTest.Reporting/BDTestReportUploadOptions.cs); [upload guide](../docs/report-server/sending-results.md) | `BDTest.Reporting`, `BDTest`. Inject a caller-owned `HttpClient`; validated options are copied. Await upload and attachment completion/cancellation before disposing client/run. `UploadAsync` returns the URI; `UploadReportAsync` also returns the optional server ID. |
| Configure hosted reports | [`AddBDTestReports`, `UseBDTestReportServer`, `MapBDTestReports`](../../BDTest.Razor.Reports/Extensions/BDTestExtensions.cs); [host setup](../docs/report-server/setup.md) | `BDTest.Razor.Reports` on .NET 10; `BDTest.Razor.Reports.Extensions`. Register before building; options validate at startup. Middleware and endpoint mapping are separate. Host owns DI lifetime, authorization and upload credentials. |
| Configure persistent storage | [file-system registration](../../BDTest.Reports.Storage.FileSystem/FileSystemReportStoreExtensions.cs), [Azure registration](../../BDTest.Reports.Storage.AzureBlob/AzureBlobReportStoreExtensions.cs), [`IReportStore`](../../BDTest.Razor.Reports/Storage/IReportStore.cs), [`IRunIndexStore`](../../BDTest.Razor.Reports/Storage/IRunIndexStore.cs), [`RunIndexPaging`](../../BDTest.Razor.Reports/Storage/RunIndexPaging.cs) | Matching storage package plus Razor host; provider namespace and `Microsoft.Extensions.DependencyInjection`. Both return `OptionsBuilder<T>`, register a shared singleton for bytes/index and validate startup options. Azure also registers attachment storage and awaited initialization; file-system attachments require separate registration. Callers dispose returned read streams; write inputs stay open. |

Public API signatures were cross-checked against core/reporting
`PublicAPI/net8.0/PublicAPI.Unshipped.txt`, the corresponding .NET Standard
baselines, and the storage packages' `PublicAPI/net10.0` baselines. Ordinary
solution builds validate the full current surface, including shipped entries
and explicit removals. Existing namespace moves (#973), fluent returns (#828)
and execution-side reporting extraction (#992) are not repeated.

## Findings and dispositions

1. **No compiled standalone execution-to-export example.** The explicit-run and
   standalone entry pages used illustrative fragments with application-only
   placeholders and omitted imports. Added a complete extracted `Standalone`
   example: options → run → scenario → immutable snapshot → current JSON → HTML.
   It is runnable without a server and names both package references. The
   standalone and run-migration guides link to it.
2. **Immutable timer guidance suggested an unavailable overload.** The
   [utilities table](../docs/advanced/utilities.md) said an empty immutable
   collection could receive a report timer, but that overload exists only for
   legacy mutable `Scenario` collections. Corrected it and documented
   `run.GetSnapshot().Timer`, which supplies the empty run's start/current time.
   The new sample compiles snapshot access; existing
   [`ReportingBoundaryTests.Empty_snapshot_uses_the_run_clock_and_freezes_the_sampled_time`](../../BDTest.Targets.Tests/ReportingBoundaryTests.cs)
   covers its behavior. No new overload is needed.
3. **Storage setup could drift without compilation.** Both provider examples
   in [persistence](../docs/report-server/persistence.md) were illustrative and
   the file-system snippet omitted the report-host namespace. Added extracted,
   complete registration methods in a `Storage` compilation project referencing
   both providers and the host. The guide explains invocation, configuration,
   provider choice and ownership. The examples do not contact Azure.
4. **Index-store IntelliSense omitted behavioral requirements.**
   `IRunIndexStore` had no XML comments despite paging, cancellation, ordering,
   upsert and idempotent deletion requirements on the persistence page. Added
   those existing requirements at the interface, including empty pages with a
   continuation token and separation from report/attachment deletion. This is
   documentation of the existing contract, not a change to store behavior.
5. **Export helper IntelliSense did not explain output/ownership.** Added XML
   documentation for descriptor defaults, completion/disposal timing, string
   output and the matching current-schema reader. `GenerateHtmlReport` now
   explains that it renders an immutable report without a live run or file I/O.
6. **Parameter order differs between export and upload.** The two export
   helpers take `(descriptor, run)`; uploader methods take
   `(run, descriptor = null, cancellationToken = default)`. Preserve these
   existing signatures and explicitly show them together in the run guide.
   Adding reversed overloads would expand the surface and risk ambiguous named
   calls without removing a demonstrated consumer failure. Async I/O remains
   cancellable; in-memory string rendering stays synchronous. The legacy
   `DeserializeAsync(stream, cancellationToken, maximumExpandedSize)` keeps its
   existing optional-argument order and caller-owned stream; the guide already
   separates legacy mutable readers from current immutable readers.
7. **Provider registration names differ.** `AddBDTestFileSystemStorage` and
   `AddAzureBlobReportStore` are both existing public names. Keep them and show
   the exact imports and calls side by side in the persistence guide. Renaming
   or adding aliases solely for symmetry would create migration work without
   improving behavior. Required cancellation tokens on storage contracts and
   optional final tokens on the uploader serve different integration levels;
   retain both conventions.
8. **Context metadata and compiler-extension installation have separate owners.**
   #1015 is implemented in #1021 and its typed context examples already compile
   for all four adapters. Include those examples in validation; no parallel
   context implementation is needed. Generator bundling (#1018) and analyzer
   bundling (#1019) remain independent open work at this baseline. This review
   does not change compiler-package references or pre-document their outcome.
9. **Upload timeout coverage assumes a successful retry finishes within 100 ms.**
   During full-suite validation, the unchanged
   [`ReportUploaderTests.Timeout_retries_but_caller_cancellation_stops`](../../BDTest.Tests/Fixtures/ReportUploaderTests.cs)
   response/timeout case returned the expected URL but observed three attempts
   instead of two. Every attempt gets the same 100 ms wall-clock budget; under
   scheduler contention a subsequent response can time out too. Filed
   [#1025](https://github.com/thomhurst/BDTest/issues/1025) with the failure,
   source evidence and acceptance criteria for deterministic deadlines while
   preserving retry, cancellation and disposal assertions. Keep that independent
   test-infrastructure correction separate from this documentation change.

Public API baselines are unchanged because only documentation and non-packable
sample projects change. Consumers need no source or binary migration for these
corrections. The validation finding in #1025 does not establish a public API
defect or require an API redesign.

## Validation

Use .NET SDK 10.0.402 and Node 24, matching CI. The sample checker extracts the
displayed fences verbatim; it does not compile separately maintained copies.

- `npm --prefix docs run check:samples`: all documentation examples, including
  adapter inheritance/composition, assembly configuration, context mapping,
  uploads, contract-only readers, host setup and the new examples.
- `dotnet run --project docs/samples/Standalone -c Release --no-build`: executes
  the documented standalone scenario and round-trips its JSON before rendering.
- `npm --prefix docs ci` and `npm --prefix docs run build`: site and link checks.
- `dotnet build BDTest.sln -c Release`: XML documentation and API-baseline checks
  for all targets; run the repository test projects with their supported runners
  as recorded in `BUILDING.md`.

Local validation on Windows with SDK 10.0.402 and Node 24.22.0:

- Release solution build: passed with zero warnings/errors.
- Sample checker: 30 blocks in 11 projects passed. The standalone smoke run
  passed one scenario, round-tripped JSON and generated HTML.
- Dependency installation and production site/link build: passed.
- All 12 test projects ran with their supported runners. Eleven passed; core
  recorded 2,250 passes, two symbolic-link privilege skips and the one timeout
  assertion failure documented in #1025. The full suite is therefore not green.

Azure registration is compiled, and the repository's fake-client storage tests
cover its contracts; this review does not provision or connect to a live Azure
account. The PR records the same validation limitation.
