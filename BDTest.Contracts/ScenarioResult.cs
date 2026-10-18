using System.Text.Json.Serialization;

namespace BDTest.Contracts;

/// <summary>An immutable snapshot of a scenario or outline row.</summary>
public sealed class ScenarioResult
{
    public string Guid { get; }
    public string ReportId { get; }
    public string StoryText { get; }
    public string ScenarioText { get; }
    public Status Status { get; }
    public IReadOnlyList<StepResult> Steps { get; }
    public IReadOnlyList<ScenarioAttemptResult> Attempts { get; }
    public IReadOnlyDictionary<string, string> ExampleValues { get; }
    public IReadOnlyList<ScenarioResult> Examples { get; }
    public IReadOnlyList<string> Tags { get; }
    public ExceptionInfo? Exception { get; }
    public string? FrameworkTestId { get; }
    public string? FileName { get; }
    public string? TestStartupInformation { get; }
    public string? TearDownOutput { get; }
    public string? CustomHtmlOutputForReport { get; }
    public string? Output { get; }
    public DateTimeOffset StartTime { get; }
    public DateTimeOffset EndTime { get; }
    public TimeSpan TimeTaken { get; }
    public TimeSpan? Timeout { get; }
    public int RetryCount { get; }
    public bool IsOutline { get; }
    public string? OutlineId { get; }
    public IReadOnlyList<AttachmentInfo> Attachments { get; }
    public IReadOnlyList<string> CustomTestInformation { get; }

    /// <summary>Whether a timeout failed a step in the final attempt.</summary>
    public bool TimedOut => Steps.Any(step => step.TimedOut);

    [JsonConstructor]
    public ScenarioResult(
        string guid,
        string reportId,
        string storyText,
        string scenarioText,
        Status status,
        IReadOnlyList<StepResult>? steps = null,
        IReadOnlyList<ScenarioAttemptResult>? attempts = null,
        IReadOnlyDictionary<string, string>? exampleValues = null,
        IReadOnlyList<ScenarioResult>? examples = null,
        IReadOnlyList<string>? tags = null,
        ExceptionInfo? exception = null,
        string? frameworkTestId = null,
        string? fileName = null,
        string? testStartupInformation = null,
        string? tearDownOutput = null,
        string? customHtmlOutputForReport = null,
        string? output = null,
        DateTimeOffset startTime = default,
        DateTimeOffset endTime = default,
        TimeSpan timeTaken = default,
        TimeSpan? timeout = null,
        int retryCount = 0,
        bool isOutline = false,
        string? outlineId = null,
        IReadOnlyList<AttachmentInfo>? attachments = null,
        IReadOnlyList<string>? customTestInformation = null)
    {
        Guid = guid ?? throw new ArgumentNullException(nameof(guid));
        ReportId = reportId ?? throw new ArgumentNullException(nameof(reportId));
        StoryText = storyText ?? throw new ArgumentNullException(nameof(storyText));
        ScenarioText = scenarioText ?? throw new ArgumentNullException(nameof(scenarioText));
        Status = Enum.IsDefined(typeof(Status), status)
            ? status : throw new ArgumentOutOfRangeException(nameof(status), status, "Expected a defined status.");
        Steps = ContractCollections.List(steps);
        Attempts = ContractCollections.List(attempts);
        ExampleValues = ContractCollections.Dictionary(exampleValues);
        Examples = ContractCollections.List(examples);
        Tags = ContractCollections.List(tags);
        Exception = exception;
        FrameworkTestId = frameworkTestId;
        FileName = fileName;
        TestStartupInformation = testStartupInformation;
        TearDownOutput = tearDownOutput;
        CustomHtmlOutputForReport = customHtmlOutputForReport;
        Output = output;
        StartTime = startTime;
        EndTime = endTime;
        TimeTaken = timeTaken;
        Timeout = timeout;
        RetryCount = retryCount;
        IsOutline = isOutline;
        OutlineId = outlineId;
        Attachments = ContractCollections.List(attachments);
        CustomTestInformation = ContractCollections.List(customTestInformation);
    }
}
