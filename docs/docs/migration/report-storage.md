---
title: Report storage interfaces
description: Move custom report storage to cancellable streams and a paged index.
---

# Report storage interfaces

The next major report-server API separates report bytes from the run index. Existing `IBDTestDataStore` implementations work for one migration release through `LegacyDataStoreAdapter`. New stores should implement `IReportStore` and `IRunIndexStore` in `BDTest.Razor.Reports.Storage`.

1. Move report JSON to `IReportStore`. Return a caller-owned stream or `null`; consume writes without closing the input. Propagate `CancellationToken` and overwrite existing keys.
2. Replace `TestRunSummary` in persistent APIs with `RunIndexEntry` and `RunIndexCounts`. Their JSON property names match existing summaries. `ReportJsonContext.Default.RunIndexEntry` supplies generated serialization metadata.
3. Implement paged `ListAsync` with newest start time first, ordinal ID tie-breaking, continuation tokens and optional exact branch/environment/tag filters. Upsert replaces an existing entry.
4. Register both `IReportStore` and `IRunIndexStore` with DI before building the host. Explicit new stores take precedence over `IBDTestDataStore`; registering only one throws.

For a staged migration:

```csharp title="Illustrative fragment"
var adapter = new LegacyDataStoreAdapter(existingStore);
builder.Services.AddSingleton<IReportStore>(adapter);
builder.Services.AddSingleton<IRunIndexStore>(adapter);
builder.Services.AddBDTestReports().BindConfiguration("BDTest");
```

Registering `builder.Services.AddSingleton<IBDTestDataStore>(existingStore)` performs the same adaptation automatically. See [Report options migration](report-options.md) for the service-registration changes. Legacy operations without cancellation tokens can only be checked before and after they finish. Legacy listing still loads all summaries, and legacy raw downloads still serialize a loaded model. Native implementations remove those limitations.

Hosts implementing `IDataRepository` directly must update `StoreData` to `(string reportId, BDTestReport model, CancellationToken cancellationToken)`, add tokens to history listing and deletion, and implement `GetDataForBulkRead`, `OpenReadAsync` and `ListAsync`. The old default interface implementation for bulk reads is removed. `OpenReadAsync` transfers stream ownership to its caller. HTTP raw downloads transfer ownership to MVC.

Hosted models now use `BDTest.Contracts`; the reader accepts schema version 1 and historical unversioned JSON. Raw downloads now preserve stored JSON exactly, including formatting and historical metadata. Report-list URLs remain unchanged. See [Persistence](../report-server/persistence.md) for contracts and a blob-store example.

## Nullable storage results

`IBDTestDataStore.GetTestData` now declares `Task<BDTestReport?>` for missing
reports. Its `StoreTestData` parameter is `BDTestReport?`: the legacy adapter
continues forwarding a JSON `null` root to the store. `MemoryCacheBdTestDataStore`
preserves that behavior and reads the null model back as null. Update custom store
signatures without adding null-forgiving operators at read sites.

`IDataRepository.GetData` and `GetDataForBulkRead`, and report/attachment
`OpenReadAsync` implementations, also declare nullable results. Test the result
before accessing report members or disposing/reading a returned stream.

Stored `TestRunSummary` and `RunIndexEntry` values preserve explicit JSON nulls for
`RecordId`, `Counts` and optional metadata. Omitted counts retain their empty-object
default. Conversion between summary and index models treats missing counts as zero.
Storage writes require a non-null record ID even though deserialization may produce
a missing one. These annotations do not change JSON names or historical formats.

## Report deletion

Replace `POST` or `DELETE /bdtest/report/delete/{id}` with `DELETE /bdtest/reports/{id}` (replace `/bdtest` with the prefix selected by `MapBDTestReports` and include any host path base). The old route is removed. The new endpoint returns 204 No Content for both deleted and missing reports, rather than a 302 redirect. Other methods at the new endpoint return 405. Invalid IDs return 400; supported IDs contain 1–128 ASCII letters, digits, dots, hyphens or underscores, excluding `.` and `..`.

Custom browser controls must send the antiforgery request token and its cookie. Use the host's configured antiforgery header (`RequestVerificationToken` by default) and same-origin credentials, then refresh the list on 204. The named `AdminAuthorizationPolicy` controls permission to delete, in addition to read access; the removed `AdminAuthorizer` option is no longer supported. See [Security](../report-server/security.md#administrators) and [Custom delete controls](../report-server/customization.md#custom-delete-controls).

Custom stores must support repeated deletes and return whether anything was deleted. The built-in repository uses that result to log missing reports through the host's `ILogger` services at Debug level. It deletes the persistent index before attachment content and report JSON, then attempts both cache invalidations even after failure or cancellation. It preserves the first error; a storage failure returns 500 and callers can retry. A failed index deletion must not delete report data. A later failure may leave unlisted data until a retry succeeds. Custom `IDataRepository` implementations should preserve the same ordering and coordinate history loading and cache-filling reads with deletion.

## Compact Razor page models

Update overrides of `Summary`, `TestTimesSummary`, `TopDefects` and `Warnings`: their `@model` is now `SummaryViewModel`, `TimingsViewModel`, `TopDefectsViewModel` and `WarningsViewModel`, respectively, in `BDTest.Razor.Reports.Models.ViewModels`. `ReportLayoutViewModel.Report` now holds compact metadata, and its `Scenarios` property is replaced by aggregate fields including `ScenarioCount`. Construct layouts with `ReportLayoutViewModel.Create(report)` when rendering full-data views yourself.

Custom `IDataRepository` implementations can keep working through the new `GetReportPages` default implementation; it builds projections without caching. Override that method for caching, and invalidate projections on deletion/replacement. Existing custom sidebar providers still receive the full report through request-local data, without adding it to the shared view models. Report URLs and query filters are unchanged. Model writes use schema version 1; historical stored JSON remains readable.

See the [view-by-view migration table](../report-server/customization.md#overriding-razor-views) and configure the separate positive `ReportPageCacheSizeLimitBytes` budget in [Caching and limits](../report-server/caching-and-limits.md#compact-page-cache).
