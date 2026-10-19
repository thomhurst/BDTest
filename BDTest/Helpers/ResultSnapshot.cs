using BDTest;
using BDTest.Contracts;
using BDTest.Test;

namespace BDTest.Helpers;

// The only boundary from mutable execution state into the report contract.
internal static class ResultSnapshot
{
    internal static ScenarioResult Create(Scenario scenario)
    {
        lock (scenario.Run?.Store.Gate ?? scenario)
            return CreateScenario(scenario);
    }

    private static ScenarioResult CreateScenario(Scenario scenario)
    {
        var steps = scenario.Steps?.Select(Create).ToArray();
        var examples = scenario.Examples?.Select(Create).ToArray();
        // Reuse the projected exception node so renderers can avoid displaying it twice.
        var exception = scenario.ExecutionException != null ? Create(scenario.ExecutionException)
            : steps?.Select(step => step.Exception).FirstOrDefault(value => value != null)
              ?? examples?.OrderBy(example => example.Status.GetOrder()).Select(example => example.Exception)
                  .FirstOrDefault(value => value != null);
        return new ScenarioResult(scenario.Guid ?? "", scenario.ReportId ?? "", scenario.GetStoryText(),
            scenario.GetScenarioText(), scenario.Status, steps,
            scenario.Attempts?.Select(attempt => new ScenarioAttemptResult(attempt.Number, attempt.Status,
                Create(attempt.Exception), Utc(attempt.StartTime), Utc(attempt.EndTime), attempt.TimeTaken, attempt.Steps)).ToArray(),
            scenario.ExampleValues, examples, scenario.Tags, exception, scenario.FrameworkTestId, scenario.FileName,
            scenario.TestStartupInformation, scenario.TearDownOutput, scenario.CustomHtmlOutputForReport, scenario.Output,
            Utc(scenario.StartTime), Utc(scenario.EndTime), scenario.TimeTaken, scenario.Timeout, scenario.RetryCount,
            scenario.IsOutline, scenario.OutlineId, scenario.Attachments?.Select(Create).ToArray(),
            scenario.CustomTestInformation?.Select(item => item.Information ?? "").ToArray());
    }

    internal static StepResult Create(Step step)
    {
        lock (step.SnapshotGate ?? step)
            return CreateStep(step);
    }

    private static StepResult CreateStep(Step step) => new(step.StepType, step.StepText ?? "", step.Status,
        step.Guid, step.ReportId, step.ScenarioId, step.IsRetry, Create(step.Exception), step.Output,
        Utc(step.StartTime), Utc(step.EndTime), step.TimeTaken, step.Timeout, step.TimedOut,
        step.Diagnostics, step.Arguments.Select(Create).ToArray(), step.Attachments?.Select(Create).ToArray(), step.SkipReason == null ? null : new Contracts.StepSkipReason(step.SkipReason.Source, step.SkipReason.AttributeType, step.SkipReason.RuleIndex, step.SkipReason.Setting));

    internal static Contracts.ScenarioDefinitionSummary Create(ScenarioDefinition definition) => new(
        definition.Guid ?? "", definition.ReportId ?? "", definition.GetStoryText(), definition.GetScenarioText(),
        definition.TestId, definition.CallerMember, definition.CallerFile,
        definition.ParameterNames?.Select(name => name ?? "").ToArray(), definition.Tags,
        definition.CustomTestInformation?.Select(item => item.Information ?? "").ToArray(), definition.ScenarioTimeout,
        definition.Steps.Select(step => new Contracts.StepDefinitionSummary(step.StepType,
            $"{step.StepType.GetValue()} {step.TextTemplate}", step.Timeout)).ToArray());

    internal static AttachmentInfo Create(Attachment attachment) => new(attachment.Id ?? "", attachment.Name ?? "",
        attachment.MimeType ?? "", attachment.Length, attachment.AttemptNumber, attachment.Uri);

    internal static Contracts.StepArgument Create(global::BDTest.StepArgument argument) => argument switch
    {
        global::BDTest.StepTable table => new Contracts.StepTable(table.Headers, table.Rows),
        global::BDTest.StepDocString text => new Contracts.StepDocString(text.Content, text.ContentType),
        _ => throw new InvalidOperationException("Unknown structured step argument.")
    };

    internal static ExceptionInfo? Create(ExceptionWrapper? exception) => Create(exception, 0, new HashSet<ExceptionWrapper>());

    private static ExceptionInfo? Create(ExceptionWrapper? exception, int depth, HashSet<ExceptionWrapper> path)
    {
        if (exception == null) return null;
        var added = path.Add(exception);
        var truncation = !added ? "Cycle" : depth >= 9 && exception.InnerExceptions?.Count > 0 ? "DepthLimit" : exception.TruncationReason;
        var children = truncation == null
            ? exception.InnerExceptions?.Select(child => Create(child, depth + 1, path)!).ToArray() : null;
        if (added) path.Remove(exception);
        return new ExceptionInfo(exception.TypeName ?? "", exception.Message ?? "", exception.StackTrace,
            exception.AsString, exception.ShortTypeName, exception.Data, exception.HResult, exception.Source,
            children, exception.IsWrapper, truncation);
    }

    internal static DateTimeOffset Utc(DateTime time) => time == default ? default
        : new DateTimeOffset(time.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(time, DateTimeKind.Utc) : time).ToUniversalTime();
}
