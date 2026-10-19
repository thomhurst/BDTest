# BDTest.Reports.Storage.AzureBlob

Persistent Azure Blob storage for BDTest reports, run indexes and attachments.
Targets .NET 10 and uses the same version as `BDTest.Razor.Reports`.

```bash
dotnet add package BDTest.Reports.Storage.AzureBlob
```

```csharp
using BDTest.Razor.Reports.Extensions;
using BDTest.Reports.Storage.AzureBlob;

builder.Services.AddBDTestReports();
builder.Services.AddAzureBlobReportStore().BindConfiguration("AzureStorage");
```

Set `AzureStorage:ConnectionString` through user secrets or the
`AzureStorage__ConnectionString` environment variable. Do not commit secrets.
The options builder binds `AzureStorageConfig` and validates it at startup.
`ReportsContainerName` defaults to `test-data`; `SummariesContainerName` defaults
to `test-runs-summaries`. Names must be distinct, 3–63 lowercase letters/digits
with optional single interior hyphens. Changing a container name selects a new
location; it does not migrate existing data.

For managed identity or other token authentication, reference `Azure.Identity`
and configure a credential in code instead of a connection string:

```csharp
builder.Services.AddAzureBlobReportStore(options =>
{
    options.ServiceUri = new Uri("https://myaccount.blob.core.windows.net");
    options.Credential = new Azure.Identity.DefaultAzureCredential();
}).BindConfiguration("AzureStorage");
```

In this case omit `ConnectionString` from configuration. `ServiceUri` must be an
absolute HTTPS URI without credentials, query or fragment. `Credential` is an
`Azure.Core.TokenCredential` object and cannot be bound from configuration.
Grant the identity data access including container creation, listing, reading,
writing and deletion (for example, Storage Blob Data Contributor).

Registration provides one singleton `AzureStorageDataStore` through
`IReportStore`, `IRunIndexStore`, `IBDTestAttachmentStore` and the legacy
`IBDTestDataStore`. Call `AddBDTestReports` too. A hosted service validates options,
creates both containers, and warms the report index before accepting requests.
Initialization and modern storage operations propagate cancellation. Storage
failures are logged and fail startup. Constructing the store does no network I/O;
when constructing it manually, await `InitializeAsync` before use.

Report JSON streams directly. Index queries scan all summary blobs, download at
most eight entries concurrently, then filter and page by stored run start time.
This is suitable for modest installations; large histories should use an indexed
`IRunIndexStore`. Existing report and summary JSON remain readable through the
source-generated BDTest serializers. Report writes and index updates are separate
operations, not transactions. Attachments share the report container under
`{reportId}/attachments/{attachmentId}`.

Retention is managed by Azure lifecycle policies. The package never installs a
policy or automatically deletes old reports. Apply the sample server's
[30-day policy](https://github.com/thomhurst/BDTest/blob/main/BDTest.ReportGenerator.RazorServer/azure-storage-lifecycle.json),
adjusting its container prefixes if you configure different names. Azure expires
each blob independently, so report, index and attachment deletion is eventual.

Hosts that copied the sample store can remove their copies and use this package's
namespace and registration extension. Default container names and
`AzureStorage:ConnectionString` remain unchanged. See the
[persistence documentation](https://bdtest.tomlonghurst.dev/docs/report-server/persistence).
