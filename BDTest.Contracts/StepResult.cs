using System.Text.Json.Serialization;

namespace BDTest.Contracts;

/// <summary>An immutable step snapshot with its public step type and captured diagnostics.</summary>
public sealed class StepResult
{
    public StepType StepType { get; }
    public string StepText { get; }
    public Status Status { get; }
    public string? Guid { get; }
    public string? ReportId { get; }
    public string? ScenarioId { get; }
    public bool IsRetry { get; }
    public ExceptionInfo? Exception { get; }
    public string? Output { get; }
    public DateTimeOffset StartTime { get; }
    public DateTimeOffset EndTime { get; }
    public TimeSpan TimeTaken { get; }
    public TimeSpan? Timeout { get; }
    public bool TimedOut { get; }
    public IReadOnlyList<string> Diagnostics { get; }
    public IReadOnlyList<StepArgument> Arguments { get; }
    public IReadOnlyList<AttachmentInfo> Attachments { get; }

    [JsonConstructor]
    public StepResult(
        StepType stepType,
        string stepText,
        Status status,
        string? guid = null,
        string? reportId = null,
        string? scenarioId = null,
        bool isRetry = false,
        ExceptionInfo? exception = null,
        string? output = null,
        DateTimeOffset startTime = default,
        DateTimeOffset endTime = default,
        TimeSpan timeTaken = default,
        TimeSpan? timeout = null,
        bool timedOut = false,
        IReadOnlyList<string>? diagnostics = null,
        IReadOnlyList<StepArgument>? arguments = null,
        IReadOnlyList<AttachmentInfo>? attachments = null)
    {
        StepType = stepType;
        StepText = stepText ?? throw new ArgumentNullException(nameof(stepText));
        Status = status;
        Guid = guid;
        ReportId = reportId;
        ScenarioId = scenarioId;
        IsRetry = isRetry;
        Exception = exception;
        Output = output;
        StartTime = startTime;
        EndTime = endTime;
        TimeTaken = timeTaken;
        Timeout = timeout;
        TimedOut = timedOut;
        Diagnostics = ContractCollections.List(diagnostics);
        Arguments = ContractCollections.List(arguments);
        Attachments = ContractCollections.List(attachments);
    }
}
