using System.Globalization;
using System.Text.Json;
using BDTest.Contracts;

namespace BDTest.Razor.Reports.Serialization;

// This compatibility boundary knows the old wire shape, never the execution assembly.
internal static class LegacyReportReader
{
    internal static BDTestReport Read(JsonElement root) => new(
        BDTestReport.CurrentSchemaVersion, Text(root, "id") ?? "", Text(root, "Version") ?? "",
        Items(root, "Scenarios", Scenario), Items(root, "NotRun", Definition),
        Text(root, "Environment"), Text(root, "Tag"), Text(root, "BranchName"), Text(root, "MachineName"),
        Optional(root, "TestTimer", timer => new ReportTimer(Date(timer, "TestsStartedAt"), Date(timer, "TestsFinishedAt"))),
        Dictionary(root, "CustomProperties"), Text(root, "AttachmentUploadTokenHash"));

    private static ScenarioResult Scenario(JsonElement value)
    {
        var steps = Items(value, "Steps", Step);
        var examples = Items(value, "Examples", Scenario);
        var exception = Optional(value, "ExecutionException", Exception)
            ?? Optional(value, "Exception", Exception)
            ?? steps.Select(step => step.Exception).FirstOrDefault(item => item != null)
            ?? examples.OrderBy(item => StatusOrder(item.Status)).Select(item => item.Exception).FirstOrDefault(item => item != null);
        return new ScenarioResult(Text(value, "Guid") ?? "", Text(value, "ReportId") ?? "",
            Text(value, "StoryText") ?? "Story Text Not Defined", Text(value, "ScenarioText") ?? "Scenario Title Not Defined",
            StatusValue(value), steps, Items(value, "Attempts", Attempt), Dictionary(value, "ExampleValues"), examples,
            Strings(value, "Tags"), exception, Text(value, "FrameworkTestId"), Text(value, "FileName"),
            Text(value, "TestStartupInformation"), Text(value, "TearDownOutput"), Text(value, "CustomHtmlOutputForReport"),
            Text(value, "Output"), Date(value, "StartTime"), Date(value, "EndTime"), Duration(value, "TimeTaken") ?? default,
            Duration(value, "Timeout"), Number(value, "RetryCount"), Boolean(value, "IsOutline"), Text(value, "OutlineId"),
            Items(value, "Attachments", Attachment), Strings(value, "CustomTestInformation"));
    }

    private static ScenarioAttemptResult Attempt(JsonElement value) => new(Number(value, "Number"), StatusValue(value),
        Optional(value, "Exception", Exception), Date(value, "StartTime"), Date(value, "EndTime"),
        Duration(value, "TimeTaken") ?? default, Items(value, "Steps", Step));

    private static StepResult Step(JsonElement value) => new(StepTypeValue(value), Text(value, "StepText") ?? "", StatusValue(value),
        Text(value, "Guid"), Text(value, "ReportId"), Text(value, "ScenarioId"), Boolean(value, "IsRetry"),
        Optional(value, "Exception", Exception), Text(value, "Output"), Date(value, "StartTime"), Date(value, "EndTime"),
        Duration(value, "TimeTaken") ?? default, Duration(value, "Timeout"), Boolean(value, "TimedOut"),
        Strings(value, "Diagnostics"), Items(value, "Arguments", Argument), Items(value, "Attachments", Attachment),
        Optional(value, "SkipReason", reason => new StepSkipReason(Text(reason, "Source") ?? "", Text(reason, "AttributeType"),
            NullableNumber(reason, "RuleIndex"), Text(reason, "Setting"))));

    private static ScenarioDefinitionSummary Definition(JsonElement value) => new(Text(value, "ScenarioId") ?? "",
        Text(value, "ReportId") ?? "", DefinitionText(value, "StoryText", "Story Text Not Defined"),
        DefinitionText(value, "ScenarioText", "Scenario Text Not Defined"), Text(value, "TestId"),
        Text(value, "CallerMember"), Text(value, "CallerFile"), Strings(value, "Parameters"), Strings(value, "Tags"),
        Strings(value, "CustomTestInformation"), Duration(value, "Timeout"),
        Items(value, "Steps", step => new StepDefinitionSummary(StepTypeValue(step), Text(step, "StepText") ?? "", Duration(step, "Timeout"))));

    private static string DefinitionText(JsonElement value, string property, string fallback)
    {
        var text = Text(value, property);
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static AttachmentInfo Attachment(JsonElement value) => new(Text(value, "Id") ?? "", Text(value, "Name") ?? "",
        Text(value, "MimeType") ?? "", Property(value, "Length") is { } length ? length.GetInt64() : 0,
        Number(value, "AttemptNumber"), Text(value, "Uri"));

    private static ExceptionInfo Exception(JsonElement value) => new(Text(value, "TypeName") ?? "", Text(value, "Message") ?? "",
        Text(value, "StackTrace"), Text(value, "AsString"), Text(value, "ShortTypeName"), Dictionary(value, "Data"),
        NullableNumber(value, "HResult"), Text(value, "Source"), Items(value, "InnerExceptions", Exception),
        Boolean(value, "IsWrapper"), Text(value, "TruncationReason"));

    private static StepArgument Argument(JsonElement value) => Text(value, "$type") switch
    {
        "table" => new StepTable(Strings(value, "Headers"), Items<IReadOnlyList<string>>(value, "Rows", row =>
            row.EnumerateArray().Select(cell => cell.GetString() ?? "").ToArray())),
        "docString" => new StepDocString(Text(value, "Content") ?? "", Text(value, "ContentType")),
        _ => throw new JsonException("Unknown structured step argument.")
    };

    private static Status StatusValue(JsonElement value)
    {
        var property = Property(value, "Status");
        if (property == null) return Status.NotRun;
        if (property.Value.ValueKind == JsonValueKind.Number)
        {
            if (!property.Value.TryGetInt64(out var number)) throw new JsonException("Expected an integer legacy status.");
            return number switch
            {
                0 => Status.Passed, 1 => Status.Failed, 2 => Status.Inconclusive,
                3 => Status.NotImplemented, 4 => Status.Skipped,
                _ => throw new JsonException("Unknown legacy status.")
            };
        }
        var name = property.Value.GetString();
        if (string.Equals(name, "Processing", StringComparison.OrdinalIgnoreCase)) return Status.Pending;
        if (Enum.TryParse<Status>(name, true, out var status) && Enum.IsDefined(status)
            && Enum.GetNames<Status>().Any(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase))) return status;
        throw new JsonException("Unknown legacy status.");
    }

    private static StepType StepTypeValue(JsonElement value)
    {
        var property = Property(value, "StepType");
        if (property == null) return StepType.Given;
        if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var number)
            && Enum.IsDefined((StepType)number)) return (StepType)number;
        if (property.Value.ValueKind == JsonValueKind.String
            && Enum.TryParse<StepType>(property.Value.GetString(), true, out var type) && Enum.IsDefined(type)) return type;
        throw new JsonException("Unknown legacy step type.");
    }

    private static int StatusOrder(Status status) => status switch
    {
        Status.Failed => 1, Status.NotImplemented => 2, Status.Inconclusive => 3,
        Status.Skipped => 4, Status.NotRun => 5, Status.Pending => 6, Status.Passed => 7,
        _ => int.MaxValue
    };

    private static JsonElement? Property(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new JsonException("Expected a report object.");
        // Match the case-insensitive, last-value-wins behavior of the historical serializer.
        JsonElement? result = null;
        foreach (var property in value.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) result = property.Value;
        return result is { ValueKind: JsonValueKind.Null } ? null : result;
    }

    private static string? Text(JsonElement value, string name) => Property(value, name)?.GetString();
    private static int Number(JsonElement value, string name) => NullableNumber(value, name) ?? 0;
    private static int? NullableNumber(JsonElement value, string name) => Property(value, name)?.GetInt32();
    private static bool Boolean(JsonElement value, string name) => Property(value, name)?.GetBoolean() ?? false;
    private static T? Optional<T>(JsonElement value, string name, Func<JsonElement, T> read) where T : class =>
        Property(value, name) is { } property ? read(property) : null;
    private static T[] Items<T>(JsonElement value, string name, Func<JsonElement, T> read) =>
        Property(value, name) is { } property ? property.EnumerateArray().Select(read).ToArray() : [];
    private static string[] Strings(JsonElement value, string name) => Items(value, name, item => item.GetString() ?? "");
    private static Dictionary<string, string>? Dictionary(JsonElement value, string name) =>
        Property(value, name) is { } property
            ? property.EnumerateObject().GroupBy(item => item.Name).ToDictionary(group => group.Key, group => group.Last().Value.GetString() ?? "")
            : null;

    private static DateTimeOffset Date(JsonElement value, string name)
    {
        if (Property(value, name) is not { } property) return default;
        // Historical unspecified DateTimes represent UTC, independent of the server's local zone.
        var time = property.GetDateTime();
        return time == default ? default
            : new DateTimeOffset(time.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : time).ToUniversalTime();
    }

    private static TimeSpan? Duration(JsonElement value, string name)
    {
        if (Property(value, name) is not { } property) return null;
        if (TimeSpan.TryParseExact(property.GetString(), [@"d\.hh\:mm\:ss\:fffffff", "c"], CultureInfo.InvariantCulture, out var result))
            return result;
        throw new JsonException("Expected a report duration.");
    }
}
