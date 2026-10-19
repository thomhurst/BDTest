# BDTest file-system report storage

`BDTest.Reports.Storage.FileSystem` provides `IReportStore` and `IRunIndexStore`
for ASP.NET Core report hosts on .NET 10. Install the same version as
`BDTest.Razor.Reports`.

```csharp
using BDTest.Razor.Reports.Extensions;
using BDTest.Reports.Storage.FileSystem;

builder.Services.AddBDTestReports();
builder.Services.AddBDTestFileSystemStorage(options =>
{
    options.DirectoryPath = Path.Combine(builder.Environment.ContentRootPath, "report-data");
    options.Retention = TimeSpan.FromDays(30);
});
```

Configure report authorization separately. The registration returns an
`OptionsBuilder<FileSystemReportStoreOptions>` supporting `BindConfiguration`.
Startup validation requires an absolute directory path and positive retention
of no more than 36500 days. The default retention is 30 days; the path is required.
The directory is created on first use, and its permissions must allow read/write/delete.

Use a dedicated directory outside the web root on a local filesystem that supports
atomic rename/replacement and exclusive file sharing. Do not use a network share
or allow untrusted users to modify the directory or its ancestors. Symbolic links
and reparse points are rejected, but path checks are not a sandbox against an
administrator changing filesystem paths concurrently.

Report IDs accept 1–128 ASCII letters, digits, hyphens or underscores. SHA-256
filenames preserve case-sensitive IDs even on case-insensitive filesystems.
Reports retain the input bytes from the current stream position; input streams
stay open. Callers must dispose returned read streams. Writes use a temporary
file in the same directory followed by atomic replacement. Readers already open
on an old version can finish. Interrupted copies leave the previous file intact.
Cancellation after publication does not roll back a committed write.

A persistent `store.lock` file coordinates cooperating instances and processes;
waiting for it retries native lock contention on Windows, Linux and macOS and
is cancellable. Do not delete this file while a host is running.
Report and index writes/deletes are separate operations, not a transaction.
Index listing scans metadata files, uses the standard BDTest filters and keyset
paging, and never reads report payloads. This store suits modest local histories.

Retention uses each report/index file's last-write UTC time independently. Reads
and lists prune expired files, and successful writes/upserts also prune. Reading
does not extend retention. There is no background timer or disk-byte quota;
idle directories are cleaned on the next read, list or write. Abandoned temporary
files are removed during pruning. This does not invalidate the report host's
existing memory cache, and it does not manage attachments.

See [persistent storage documentation](https://thomhurst.github.io/BDTest/docs/report-server/persistence).
