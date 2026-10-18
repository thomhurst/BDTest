# Report JSON migration

BDTest and BDTest.Razor.Reports use System.Text.Json and no longer depend on
Newtonsoft.Json. The core package still targets netstandard2.0 and references
the System.Text.Json package.

## Reading and writing reports

Use `BDTestJsonHelper.Serialize(report)` and `BDTestJsonHelper.Deserialize(json)`
from `BDTest.Helpers`. The asynchronous reader accepts a UTF-8 stream and leaves
that stream open. Both readers accept historical Newtonsoft reports, including
`$id`/`$ref` references and `$values` collection wrappers. They reject missing,
duplicate, or cyclic references. Both readers limit expanded legacy JSON to
100 MiB by default, including reads from persistent storage. Callers can set
`maximumExpandedSize` explicitly for a different storage budget. The report upload
endpoint uses its existing 10 MiB upload budget.

Current writers emit plain JSON. Shared steps, metadata, and example values are
written as independent values. Consumers must not depend on deserialized object
identity. Outline examples continue to identify their parent through `OutlineId`.

Property names remain unchanged, including lowercase `id` at the report root.
Statuses and step types are written as enum names. Historical numeric enum values
remain readable. Step, scenario, and attempt `TimeTaken` values retain the
`d.hh:mm:ss:fffffff` format. Nullable timeouts and the timer's derived
`ElapsedTime` retain the standard invariant TimeSpan format.

`BDTestJsonContext` provides generated metadata for report types, and
`ReportJsonContext` in BDTest.Razor.Reports provides metadata for stored run
summaries. Use their `JsonTypeInfo` properties when calling System.Text.Json
directly. The compatibility readers must be used for historical reports with
references. Serialization does not consult global Newtonsoft settings or host
MVC JSON options.

## Hosting the report server

`AddBDTestReports()` registers BDTest's controllers and services
without replacing MVC input or output formatters or changing host JSON options.
The upload endpoint uses its own report reader, and raw report downloads use the
same generated metadata as the test client and persistent storage. Existing
historical reports still render through the report server and Azure storage.

Hosts that previously relied on BDTest enabling Newtonsoft for their own
controllers must reference Microsoft.AspNetCore.Mvc.NewtonsoftJson and call
`AddNewtonsoftJson()` themselves. Applications that use Newtonsoft independently
must also add their own package reference rather than relying on BDTest's former
transitive dependency.

BDTest JSON converter classes now derive from System.Text.Json converters.
Replace custom Newtonsoft serialization code and attributes with System.Text.Json
equivalents, and supply any additional application types through your own
generated context. BDTest's report writer deliberately uses its own fixed wire
contract.

The report serializer can run with System.Text.Json reflection disabled. This
does not establish trimming or NativeAOT support for the entire test engine:
automatic step-text generation still uses reflection and currently produces
IL2075 trimming diagnostics in `StepTextHelper.GetExpressionValue`.
