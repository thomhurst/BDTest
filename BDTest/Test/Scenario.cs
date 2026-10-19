using BDTest.Attributes;
using BDTest.Constants;
using BDTest.Output;
using BDTest.Metadata;
using System.Text.Json.Serialization;

namespace BDTest.Test;

public class Scenario
{
    [JsonIgnore] internal TestMethodMetadata? TestMetadata { get; private set; }

    /// <summary>Gets the owning run, or null for a deserialized report.</summary>
    [JsonIgnore] public BDTestRun? Run { get; private set; }

    [JsonIgnore] internal List<Step> ExecutionSteps => Steps ?? throw new InvalidOperationException("The scenario has no steps.");
    [JsonIgnore] internal List<Scenario> ExecutionExamples => Examples ?? throw new InvalidOperationException("The outline has no examples.");
    [JsonIgnore] internal List<ScenarioAttempt> ExecutionAttempts => Attempts ?? throw new InvalidOperationException("The scenario has no attempt collection.");
    [JsonIgnore] internal List<Attachment> ExecutionAttachments => Attachments ?? throw new InvalidOperationException("The scenario has no attachment collection.");

    [JsonIgnore] internal BDTestRun ExecutionRun => Run ?? throw new InvalidOperationException("A report scenario cannot be executed.");

    [JsonInclude] public string? Guid { get; internal set; }
    [JsonInclude] public string? ReportId { get; internal set; }
    [JsonInclude] public DateTime StartTime { get; internal set; }

    [JsonInclude] public DateTime EndTime { get; internal set; }

    [JsonInclude] public string? FileName { get; internal set; }
        
    [JsonInclude]
    public string? TestStartupInformation
    {
        get => StartupOutput.Value;
        internal set => StartupOutput.Value = value;
    }

    [JsonIgnore] internal BufferedOutput StartupOutput { get; set; } = new();
    [JsonIgnore] internal BufferedOutput TeardownOutputBuffer { get; } = new();

    [JsonIgnore] public ExceptionWrapper? Exception => ExecutionException
        ?? Steps?.Select(step => step.Exception).FirstOrDefault(exception => exception != null)
        ?? Examples?.OrderBy(example => example.Status.GetOrder()).Select(example => example.Exception)
            .FirstOrDefault(exception => exception != null);

    [JsonInclude] internal ExceptionWrapper? ExecutionException { get; set; }
    [JsonInclude] public bool IsOutline { get; internal set; }
    [JsonInclude] public string? OutlineId { get; internal set; }
    [JsonInclude] public Dictionary<string, string>? ExampleValues { get; internal set; } = new();
    [JsonInclude] public List<Scenario>? Examples { get; internal set; } = new();
        
    [JsonInclude]
    public string? TearDownOutput
    {
        get => TeardownOutputBuffer.Value;
        internal set => TeardownOutputBuffer.Value = value;
    }
        
    [JsonInclude] public string? CustomHtmlOutputForReport { get; internal set; }

    /// <summary>Files captured outside steps, distinguished by Attachment.AttemptNumber.</summary>
    [JsonInclude] public List<Attachment>? Attachments { get; internal set; } = new();

    [JsonIgnore] internal BDTestRuntimeInformation? RuntimeInformation { get; set; }

    [JsonConstructor]
    internal Scenario()
    {
    }

    internal bool AlreadyExecuted;

    internal Scenario(List<Step> steps, ScenarioDefinition testDetails, Scenario? outline = null,
        Dictionary<string, string>? exampleValues = null)
    {
        Run = testDetails.Run;
        TestMetadata = outline?.TestMetadata ?? testDetails.TestMetadata;
        Run.ThrowIfDisposed();
        ReportId = Run.ReportId;
        Timeout = testDetails.ScenarioTimeout;
        Guid = testDetails.Guid == outline?.Guid ? System.Guid.NewGuid().ToString("N") : testDetails.Guid;
        FrameworkTestId = testDetails.TestId;
        OutlineId = outline?.Guid;
        ExampleValues = exampleValues ?? new Dictionary<string, string>();

        RuntimeInformation = new BDTestRuntimeInformation
        {
            CallerFile = outline?.RuntimeInformation?.CallerFile ?? testDetails.CallerFile,
            CallerMember = outline?.RuntimeInformation?.CallerMember ?? testDetails.CallerMember,
            BdTestBase = testDetails.BdTestBase
        };
            
        if (testDetails.Guid != null) Run.Store.NotRun.TryRemove(testDetails.Guid, out _);

        StoryText = testDetails.StoryText;
        ScenarioText = outline?.ScenarioText ?? testDetails.ScenarioText;
        CustomTestInformation = outline?.CustomTestInformation ?? testDetails.CustomTestInformation;
        Tags = outline?.Tags ?? testDetails.Tags;

        FileName = RuntimeInformation.CallerFile;
            
        Steps = steps;
        outline?.ExecutionExamples.Add(this);
        Run.Store.AddScenario(this);
    }

    [JsonInclude]
    public string? FrameworkTestId { get; internal set; }

    /// <summary>The timeout budget for the steps in each execution attempt, or null for no limit.</summary>
    [JsonInclude] public TimeSpan? Timeout { get; internal set; }

    /// <summary>Whether a BDTest timeout failed a step in the current attempt.</summary>
    public bool TimedOut => Steps?.Any(step => step.TimedOut) == true;

    [JsonInclude] public List<Step>? Steps { get; internal set; } = new();

    [JsonInclude] public string? Output { get; internal set; }

    [JsonInclude]
    [JsonConverter(typeof(StatusConverter))]
    public Status Status { get; internal set; } = Status.NotRun;

    [JsonInclude] internal StoryText? StoryText { get; set; }

    [JsonInclude] internal ScenarioText? ScenarioText { get; set; }
        
    [JsonInclude] public TestInformationAttribute[]? CustomTestInformation { get; internal set; }

    /// <summary>Combined story and scenario tags, distinct ignoring case.</summary>
    [JsonInclude]
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public string[] Tags
    {
        get => _tags;
        internal set => _tags = value ?? Array.Empty<string>();
    }
    private string[] _tags = Array.Empty<string>();
        
    [JsonIgnore] internal bool ShouldRetry { get; set; }
    [JsonInclude] public int RetryCount { get; internal set; }

    /// <summary>Completed execution attempts, including the initial and final attempts, in execution order.</summary>
    /// <remarks>Reports created before attempt tracking was introduced have an empty collection.</remarks>
    [JsonInclude] public List<ScenarioAttempt>? Attempts { get; internal set; } = new();

    /// <summary>The executing test instance, or null if it has been collected after execution.</summary>
    /// <remarks>Completed scenarios retain only a weak reference to the test instance.</remarks>
    [JsonIgnore] public BDTestBase? BdTestBaseClass => RuntimeInformation?.BdTestBase;

    internal void ReleaseRuntimeReferences()
    {
        foreach (var step in Steps ?? new())
        {
            step.ReleaseRuntimeReferences();
        }
        RuntimeInformation?.ReleaseRuntimeReferences();
    }

    public string GetScenarioText()
    {
        return ScenarioText?.Scenario ?? "Scenario Title Not Defined";
    }

    public string GetStoryText()
    {
        return StoryText?.Story ?? DefaultValues.StoryTextNotDefined;
    }

    [JsonConverter(typeof(TimespanConverter))]
    [JsonInclude]
    public TimeSpan TimeTaken { get; internal set; }
}
