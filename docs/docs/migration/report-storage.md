---
title: Report storage interfaces
description: Move custom report storage to cancellable streams and a paged index.
---

# Report storage interfaces

The next major report-server API separates report bytes from the run index. Existing `IBDTestDataStore` implementations work for one migration release through `LegacyDataStoreAdapter`. New stores should implement `IReportStore` and `IRunIndexStore` in `BDTest.Razor.Reports.Storage`.

1. Move report JSON to `IReportStore`. Return a caller-owned stream or `null`; consume writes without closing the input. Propagate `CancellationToken` and overwrite existing keys.
2. Replace `TestRunSummary` in persistent APIs with `RunIndexEntry` and `RunIndexCounts`. Their JSON property names match existing summaries. `ReportJsonContext.Default.RunIndexEntry` supplies generated serialization metadata.
3. Implement paged `ListAsync` with newest start time first, ordinal ID tie-breaking, continuation tokens and optional exact branch/environment/tag filters. Upsert replaces an existing entry.
4. Set both `options.ReportStore` and `options.RunIndexStore` at startup. Explicit new stores take precedence over `DataStore`; configuring only one throws.

For a staged migration:

```csharp
var adapter = new LegacyDataStoreAdapter(existingStore);
app.UseBDTestReportServer(options =>
{
    options.ReportStore = adapter;
    options.RunIndexStore = adapter;
});
```

Keeping `options.DataStore = existingStore` performs the same adaptation automatically. Legacy operations without cancellation tokens can only be checked before and after they finish. Legacy listing still loads all summaries, and legacy raw downloads still serialize a loaded model. Native implementations remove those limitations.

Hosts implementing `IDataRepository` directly must update `StoreData` to `(string reportId, BDTestOutputModel model, CancellationToken cancellationToken)`, add tokens to history listing and deletion, and implement `GetDataForBulkRead`, `OpenReadAsync` and `ListAsync`. The old default interface implementation for bulk reads is removed. `OpenReadAsync` transfers stream ownership to its caller. HTTP raw downloads transfer ownership to MVC.

The report JSON schema and historical JSON reader do not change. Raw downloads now preserve stored JSON exactly, including formatting and historical metadata. Report-list URLs remain unchanged. See [Persistence](../report-server/persistence.md) for contracts and a blob-store example.
