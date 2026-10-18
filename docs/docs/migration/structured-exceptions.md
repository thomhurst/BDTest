---
title: Structured exceptions
description: Full exception identities, nested diagnostics and root-cause grouping in BDTest reports.
---

# Structured exceptions

`ExceptionWrapper.TypeName` now contains the full type name, such as `System.InvalidOperationException`. Use `ShortTypeName` for a compact display name. Update code that compares `TypeName` to a short string:

```csharp
// Identity checks distinguish types in different namespaces.
bool isInvalidOperation = exception.TypeName == typeof(InvalidOperationException).FullName;

// Display names remain available separately.
string label = exception.ShortTypeName;
```

## Exception trees

Step, scenario and retry-attempt exceptions share the same structure. `InnerExceptions` contains the ordinary inner exception, or every child of an `AggregateException` in its original order. Each node includes `TypeName`, `ShortTypeName`, `Message`, `StackTrace`, `Source`, `HResult` and `Data`.

`Data` contains string representations of the exception's data keys and values. Null values become empty strings. Keys that produce the same string receive suffixes such as ` (2)`. A throwing formatter produces `[Value could not be formatted]`. Exception objects stored in `Data` use their full type name to avoid recursively formatting a cyclic graph. Custom assertion properties outside `Exception.Data` are not inspected.

Data is optional diagnostics. A null or throwing `Data` getter, or a dictionary that fails during enumeration, does not replace the original failure. Capture keeps any entries read before the failure.

`Source` is also optional diagnostics. If a custom exception's `Source` getter throws, capture leaves `Source` null and preserves the original failure and retry behavior.

Capture stops after ten nodes on each branch. A node with omitted children has `TruncationReason` set to `DepthLimit`. A repeated exception on the current branch becomes a terminal node marked `Cycle`. The same exception in separate aggregate branches remains present in each branch.

`AsString` remains available and normally contains the original `Exception.ToString()` output. For truncated graphs or a throwing formatter, it contains bounded diagnostics instead. Aggregate messages recursively format their children too, so a truncated aggregate uses an explanatory message instead of calling that formatter.

## Rendering and grouping

Hosted Razor reports and standalone HTML reports show an encoded, collapsible tree. The outer exception starts expanded; child exceptions start collapsed. Original diagnostic text remains available in a separate collapsed section, including for historical reports.

Invocation wrappers and single-child aggregates retain their diagnostic nodes and have `IsWrapper` set to `true`. Reports show a wrapper marker. They are traversed when finding root causes rather than discarded, so messages, stack traces and aggregate branches remain available.

`GetRootCauses()` returns every terminal cause. It returns the exception itself when there are no children. Top defects uses terminal causes for its message/location and exception-type grouping modes; failing-step grouping still counts each scenario once. Exception-type grouping and flakiness count a scenario once per root cause type. Aggregates with different cause types contribute to each corresponding group. Standalone reports retain Copy and Wrap controls for original exception text.

The flakiness Failures table retains distinct root cause type/message pairs and counts each pair once per failed run. Messages are trimmed for grouping. Multiple messages of the same type remain visible without inflating the most common failure count.

## Historical reports

The [report reader](../reports/json-data.md) still accepts older JSON without the new fields. Missing `InnerExceptions` and `Data` default to empty collections. `ShortTypeName` derives from `TypeName` when absent. Missing `HResult` remains null.

Historical short type names cannot be expanded into namespace-qualified identities, and inner exceptions are not reconstructed from `AsString`. Historical reports retain their original names and diagnostic text. New report fields round-trip through the source-generated `BDTestJsonContext`.
