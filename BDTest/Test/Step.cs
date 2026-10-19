using BDTest.Contracts;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Threading;
using BDTest.Engine;
using BDTest.Helpers;
using BDTest.Output;
using BDTest.Settings;
using BDTest;
using System.Text.Json.Serialization;

namespace BDTest.Test;

public class Step
{
    [JsonIgnore]
    internal Runnable? Runnable { get; private set; }
        
    [JsonInclude]
    public string? Guid { get; internal set; }
        
    [JsonInclude]
    public string? ReportId { get; internal set; }
        
    [JsonInclude]
    public string? ScenarioId { get; internal set; }

    [JsonInclude]
    public DateTime StartTime { get; internal set; }

    [JsonInclude]
    public DateTime EndTime { get; internal set; }
        
    [JsonInclude]
    [JsonConverter(typeof(JsonStringEnumConverter<StepType>))]
    internal StepType StepType { get; }
    private string StepPrefix => StepType.GetValue();

    [JsonInclude]
    public string? Output { get; internal set; }

    [JsonConverter(typeof(TimespanConverter))]
    [JsonInclude]
    public TimeSpan TimeTaken { get; internal set; }

    [JsonInclude]
    [JsonConverter(typeof(StatusConverter))]
    public Status Status { get; internal set; } = Status.NotRun;

    [JsonInclude]
    public ExceptionWrapper? Exception { get; internal set; }

    /// <summary>The matched skip rule or debug setting, or null when no condition skipped this step.</summary>
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StepSkipReason? SkipReason
    {
        get => _skipReason;
        internal set
        {
            // Publication and retry clears must not race a result snapshot.
            lock (SnapshotGate ?? this)
                _skipReason = value;
        }
    }

    private StepSkipReason? _skipReason;

    /// <summary>The timeout for this step, or null for no step-specific limit.</summary>
    [JsonInclude]
    public TimeSpan? Timeout { get; internal set; }

    /// <summary>True when a step or scenario timeout failed this step.</summary>
    [JsonInclude]
    public bool TimedOut { get; internal set; }

    /// <summary>Non-fatal errors encountered while rendering this attempt's step text.</summary>
    [JsonInclude]
    public List<string>? Diagnostics { get; internal set; } = new();

    /// <summary>Structured values captured for this execution attempt. Historical reports default to an empty list.</summary>
    [JsonInclude]
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public IReadOnlyList<StepArgument> Arguments
    {
        get => _arguments;
        internal set => _arguments = Array.AsReadOnly((value ?? Array.Empty<StepArgument>()).ToArray());
    }

    private IReadOnlyList<StepArgument> _arguments = Array.AsReadOnly(Array.Empty<StepArgument>());

    [JsonIgnore]
    internal List<Func<StepArgument>> AttachedArguments { get; } = new();

    /// <summary>Captured files from all attempts, distinguished by Attachment.AttemptNumber.</summary>
    [JsonInclude] public List<Attachment>? Attachments { get; internal set; } = new();

    [JsonIgnore] private RunOptions Options => _options ?? throw new InvalidOperationException("A report step cannot be executed.");
    private readonly RunOptions? _options;
    [JsonIgnore] internal object? SnapshotGate { get; }

    internal List<Attachment> ExecutionAttachments => Attachments ?? throw new InvalidOperationException("The step has no attachment collection.");
    private List<string> ExecutionDiagnostics => Diagnostics ?? throw new InvalidOperationException("The step has no diagnostics collection.");
    private Runnable ExecutionRunnable => Runnable ?? throw new InvalidOperationException("A report step cannot be executed.");

    /// <summary>The effective token for this step and its hooks; cancelled during timeout or scenario cancellation.</summary>
    [JsonIgnore] internal CancellationToken CancellationToken { get; private set; }

    private string? _stepTextTemplate;
    private string? _generatedStepText;
    private IReadOnlyList<string> _templateDiagnostics = Array.Empty<string>();

    internal Step(Runnable runnable, StepType stepType, string? scenarioId, string? reportId, BDTestRun run)
        : this(new StepDefinition(runnable, stepType), scenarioId, reportId, run)
    {
    }

    private Step(Runnable runnable, StepType stepType, string? scenarioId, string? reportId, BDTestRun run, string textTemplate,
        IReadOnlyList<string> templateDiagnostics)
    {
        _options = (run ?? throw new ArgumentNullException(nameof(run))).Options;
        SnapshotGate = run.Store.Gate;
        Guid = System.Guid.NewGuid().ToString("N");
        ScenarioId = scenarioId;
        Runnable = runnable;
        StepType = stepType;
        ReportId = reportId;
        // Construction and reporting retain placeholders without running user argument expressions.
        _stepTextTemplate = textTemplate;
        _generatedStepText = _stepTextTemplate;
        _templateDiagnostics = templateDiagnostics;
        foreach (var diagnostic in _templateDiagnostics) AddDiagnostic(diagnostic);
        SetStepText();
    }

    internal Step(StepDefinition definition, string? scenarioId, string? reportId, BDTestRun run)
        : this(definition.Runnable, definition.StepType, scenarioId, reportId, run, definition.TextTemplate, definition.TemplateDiagnostics)
    {
        OverriddenStepText = definition.OverriddenStepText;
        Timeout = definition.Timeout;
        // The definition already rendered the template; copying readers must not run user code.
        _explicitArguments = definition.ExplicitArguments?.ToArray();
        AttachedArguments.AddRange(definition.AttachedArguments);
    }

    [JsonConstructor]
    internal Step(StepType stepType)
    {
        StepType = stepType;
    }

    [JsonInclude]
    public string? StepText { get; internal set; }

    [JsonIgnore]
    internal Func<string?>? OverriddenStepText { get; set; }

    private IReadOnlyList<Func<object?>>? _explicitArguments;
    [JsonIgnore]
    internal IReadOnlyList<Func<object?>>? ExplicitArguments
    {
        get => _explicitArguments;
        set
        {
            _explicitArguments = value;
            if (Runnable == null) return;
            var diagnostics = new List<string>();
            _stepTextTemplate = Runnable.Definition.GetText(explicitArguments: value, diagnostic: diagnostics.Add);
            _templateDiagnostics = diagnostics.ToArray();
            foreach (var diagnostic in _templateDiagnostics) AddDiagnostic(diagnostic);
            _generatedStepText = _stepTextTemplate;
            StepText = $"{StepPrefix} {_stepTextTemplate}";
        }
    }

    [JsonIgnore] internal Dictionary<string, string>? ExampleValues { get; set; }

    private string SubstituteExampleValues(string text) => ExampleValues == null ? text
        : Regex.Replace(text, "<([^<>]+)>", match => ExampleValues.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);

    [JsonIgnore]
    public bool IsRetry { get; set; }

    internal void ReleaseRuntimeReferences()
    {
        CancellationToken = default;
        Runnable = null;
        OverriddenStepText = null;
        ExplicitArguments = null;
        AttachedArguments.Clear();
    }

    internal void ResetData()
    {
        _generatedStepText = _stepTextTemplate;
        StepText = SubstituteExampleValues($"{StepPrefix} {_stepTextTemplate}");
        lock (SnapshotGate ?? this)
            ExecutionDiagnostics.Clear();
        foreach (var diagnostic in _templateDiagnostics) AddDiagnostic(diagnostic);
        Arguments = Array.Empty<StepArgument>();
        TimedOut = false;
        SkipReason = null;
        Exception = null;
        Output = null;
        StartTime = DateTime.MinValue;
        EndTime = DateTime.MinValue;
        TimeTaken = TimeSpan.Zero;
        Status = Status.NotRun;
        IsRetry = true;
    }

    internal void SetStepText(bool evaluateArguments = false)
    {
        var invokedStepText = OverriddenStepText?.Invoke();
        var generatedStepText = _generatedStepText;
        var diagnostics = new List<string>();
        var arguments = new List<StepArgument>();
        if (evaluateArguments)
        {
            // A nonempty text override retains its existing behavior: inline readers are bypassed.
            if (string.IsNullOrWhiteSpace(invokedStepText))
            {
                generatedStepText = ExecutionRunnable.Definition.GetText(true, diagnostics.Add, ExplicitArguments, Options.Converters, arguments.Add);
            }
            foreach (var reader in AttachedArguments)
            {
                StepTextHelper.GetArgumentValue(reader, "attachment", diagnostics.Add, Options.Converters, arguments.Add);
            }
        }
        // Argument readers can ignore cancellation too. Publish only while this callback owns capture.
        ExecutionOutputScope.Commit(() =>
        {
            lock (SnapshotGate ?? this)
            {
                if (evaluateArguments)
                {
                    _generatedStepText = generatedStepText;
                    foreach (var diagnostic in diagnostics) AddDiagnostic(diagnostic);
                    Arguments = arguments;
                }
                StepText = SubstituteExampleValues($"{StepPrefix} {(string.IsNullOrWhiteSpace(invokedStepText) ? _generatedStepText : invokedStepText)}");
            }
        });
    }

    private void AddDiagnostic(string diagnostic)
    {
        // Match snapshot reads without holding the gate while user readers or converters execute.
        lock (SnapshotGate ?? this)
            if (!ExecutionDiagnostics.Contains(diagnostic)) ExecutionDiagnostics.Add(diagnostic);
    }

    private StepSkipReason? EvaluateSkipReason()
    {
        // The run owns the fixed skip-rule registrations for every attempt.
        var rules = Options.SkipStepRules;
        foreach (var attribute in ExecutionRunnable.Definition.SkipAttributes)
        {
            for (var index = 0; index < rules.Count; index++)
            {
                var rule = rules[index];
                if (rule.AssociatedSkipAttributeType == attribute.GetType() && rule.Condition(attribute))
                {
                    return new StepSkipReason("Rule", attribute.GetType().FullName, index);
                }
            }
        }

        return (StepType == StepType.When || StepType == StepType.ButWhen || StepType == StepType.AndWhen)
            && Options.ShouldSkipWhenStep
            ? new StepSkipReason("DebugSetting", setting: "Debug.ShouldSkipWhenStep")
            : null;
    }

    internal async Task Execute(BDTestBase? test, Scenario scenario, ExecutionCancellation attempt)
    {
        using var cancellation = new ExecutionCancellation(scenario, this, attempt);
        var token = cancellation.Token;
        CancellationToken = token;
        using var attachmentScope = new AttachmentScope(scenario, this);
        SkipReason = null;
        var beforeHooks = Options.BeforeStep;
        var afterHooks = Options.AfterStep;
        Exception? executionException = null;
        var timer = Options.Clock.StartTimer();
        try
        {
            StartTime = Options.Clock.UtcNow;
            Status = Status.Pending;
            if (beforeHooks != null)
                foreach (Func<StepResult, CancellationToken, Task> hook in beforeHooks.GetInvocationList())
                    await cancellation.RunAsync(() => hook(ResultSnapshot.Create(this), token));
            if (test != null)
                await cancellation.RunAsync(() => test.OnBeforeStep(ResultSnapshot.Create(this), token));

            await cancellation.RunAsync(() =>
            {
                var reason = EvaluateSkipReason();
                ExecutionOutputScope.Commit(() => SkipReason = reason);
                return Task.CompletedTask;
            });
            if (SkipReason != null)
            {
                SetStepText();
                StepText = $"[Skipped] {StepText}";
                Status = Status.Skipped;
                return;
            }

            // Render and invoke within the same cancellation/output boundary.
            var runnable = ExecutionRunnable;
            await cancellation.RunAsync(() =>
            {
                SetStepText(evaluateArguments: true);
                token.ThrowIfCancellationRequested();
                return runnable.Run(token);
            });
            Status = Status.Passed;
        }
        catch (Exception e)
        {
            executionException = e;
            SetExceptionResult(e);
            if (Status == Status.Failed)
                await Console.Out.WriteLineAsync($"{Environment.NewLine}{e.GetType().Name} on '{StepPrefix}' step executing '{StepText}'");
            throw;
        }
        finally
        {
            EndTime = Options.Clock.UtcNow;
            TimeTaken = timer.Elapsed;
            Output = TestOutputData.Instance.ToString().Trim();
            // A later cleanup timeout must not reclassify an earlier failure.
            var timedOut = TimedOut;
            var scenarioTimedOut = scenario.AttemptTimedOut;
            try
            {
                var hookException = await RunAfterHooks(test, afterHooks, cancellation);
                if (executionException == null && hookException != null)
                {
                    SetExceptionResult(hookException);
                    ExceptionDispatchInfo.Capture(hookException).Throw();
                }
            }
            finally
            {
                if (executionException != null)
                {
                    TimedOut = timedOut;
                    scenario.AttemptTimedOut = scenarioTimedOut;
                }
                Output = TestOutputData.Instance.ToString().Trim();
                TestOutputData.ClearCurrentTaskData();
            }
        }
    }

    private async Task<Exception?> RunAfterHooks(BDTestBase? test, Func<StepResult, CancellationToken, Task>? globalHooks,
        ExecutionCancellation cancellation)
    {
        Exception? firstException = null;
        if (test != null) await Invoke(() => test.OnAfterStep(ResultSnapshot.Create(this), CancellationToken));
        if (globalHooks != null)
            foreach (Func<StepResult, CancellationToken, Task> hook in globalHooks.GetInvocationList())
                await Invoke(() => hook(ResultSnapshot.Create(this), CancellationToken));
        return firstException;

        async Task Invoke(Func<Task> hook)
        {
            try { await cancellation.RunAsync(hook, cleanup: true); }
            catch (Exception e)
            {
                firstException ??= e;
                await Console.Out.WriteLineAsync($"After-step hook failed: {e}");
            }
        }
    }

    private void SetExceptionResult(Exception exception)
    {
        Status = ExceptionOutcomeClassifier.Classify(exception, Options, TimedOut || (exception is OperationCanceledException && CancellationToken.IsCancellationRequested));
        if (Status == Status.Failed || Status == Status.NotImplemented)
            Exception = new ExceptionWrapper(exception);
    }

    internal static Task CancelAndObserve(Task execution, CancellationTokenSource cancellation)
    {
        // Observe abandoned work independently: cancellation callbacks may never return.
        var observation = ObserveExecution(execution);
        _ = CancelAndDispose(cancellation, observation);
        return observation;
    }

    private static async Task ObserveExecution(Task execution)
    {
        try
        {
            await execution.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The caller already received the timeout.
        }
    }

    private static async Task CancelAndDispose(CancellationTokenSource cancellation, Task observation)
    {
        try
        {
            await Task.Run(() => cancellation.Cancel()).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The timeout remains the primary failure.
        }
        finally
        {
            // Both the step and its callbacks can still use the source until they finish.
            await observation.ConfigureAwait(false);
            cancellation.Dispose();
        }
    }
}
