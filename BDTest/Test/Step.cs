using BDTest.Contracts;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Threading;
using BDTest.Engine;
using BDTest.Helpers;
using BDTest.Output;
using BDTest.Settings;
using BDTest.Test.Steps;
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

    private string? _stepTextTemplate;
    private string? _generatedStepText;

    internal Step(Runnable runnable, StepType stepType, string? scenarioId, string? reportId, BDTestRun run)
        : this(runnable, stepType, scenarioId, reportId, run, runnable.Definition.GetText())
    {
    }

    private Step(Runnable runnable, StepType stepType, string? scenarioId, string? reportId, BDTestRun run, string textTemplate)
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
        SetStepText();
    }

    internal Step(StepDefinition definition, string? scenarioId, string? reportId, BDTestRun run)
        : this(definition.Runnable, definition.StepType, scenarioId, reportId, run, definition.TextTemplate)
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
            _stepTextTemplate = Runnable.Definition.GetText(explicitArguments: value);
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
        if (evaluateArguments)
        {
            var arguments = new List<StepArgument>();
            // A nonempty text override retains its existing behavior: inline readers are bypassed.
            if (string.IsNullOrWhiteSpace(invokedStepText))
            {
                _generatedStepText = ExecutionRunnable.Definition.GetText(true, AddDiagnostic, ExplicitArguments, Options.Converters, arguments.Add);
            }
            foreach (var reader in AttachedArguments)
            {
                StepTextHelper.GetArgumentValue(reader, "attachment", AddDiagnostic, Options.Converters, arguments.Add);
            }
            Arguments = arguments;
        }

        if (!string.IsNullOrWhiteSpace(invokedStepText))
        {
            StepText = SubstituteExampleValues($"{StepPrefix} {invokedStepText}");
            return;
        }

        StepText = SubstituteExampleValues($"{StepPrefix} {_generatedStepText}");
    }

    private void AddDiagnostic(string diagnostic)
    {
        // Match snapshot reads without holding the gate while user readers or converters execute.
        lock (SnapshotGate ?? this)
            ExecutionDiagnostics.Add(diagnostic);
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

    internal async Task Execute(BDTestBase? test, TimeSpan? scenarioTimeout = null, TimeSpan? remainingScenarioTime = null,
        Scenario? scenario = null)
    {
        using var attachmentScope = scenario == null ? null : new AttachmentScope(scenario, this);
        SkipReason = null;

        // Keep both subscriptions stable for this step, including its after hooks.
        var beforeHooks = Options.BeforeStep;
        var afterHooks = Options.AfterStep;
        Exception? executionException = null;
        var beforeHooksCompleted = false;
        var timer = Options.Clock.StartTimer();
        try
        {
            StartTime = Options.Clock.UtcNow;
            Status = Status.Pending;

            if (beforeHooks != null)
            {
                foreach (Func<StepResult, Task> hook in beforeHooks.GetInvocationList())
                {
                    await hook(ResultSnapshot.Create(this));
                }
            }
            if (test != null)
            {
                await test.OnBeforeStep(ResultSnapshot.Create(this));
            }

            beforeHooksCompleted = true;

            SkipReason = EvaluateSkipReason();
            if (SkipReason != null)
            {
                SetStepText();
                StepText = $"[Skipped] {StepText}";
                Status = Status.Skipped;
                return;
            }

            // Render once per attempt, after hooks and skip checks, immediately before invocation.
            SetStepText(evaluateArguments: true);

            var remaining = remainingScenarioTime - timer.Elapsed;
            var scenarioLimit = remaining.HasValue && (!Timeout.HasValue || remaining.Value <= Timeout.Value);
            var limit = scenarioLimit ? remaining : Timeout;
            if (limit.HasValue)
            {
                await RunWithTimeout(limit.Value, scenarioLimit ? "Scenario" : "Step", (scenarioLimit ? scenarioTimeout : Timeout) ?? throw new InvalidOperationException("The configured timeout is missing."));
            }
            else
            {
                await ExecutionRunnable.Run();
            }
            Status = Status.Passed;
        }
        catch (Exception e)
        {
            // Preserve failures raised within budget even if after hooks later exceed it.
            if (!beforeHooksCompleted && remainingScenarioTime.HasValue && timer.Elapsed >= remainingScenarioTime.Value)
            {
                e = CreateTimeoutException("Scenario", scenarioTimeout ?? throw new InvalidOperationException("The scenario timeout is missing."));
            }
            executionException = e;
            SetExceptionResult(e);
            if (Status == Status.Failed)
            {
                await Console.Out.WriteLineAsync($"{Environment.NewLine}{e.GetType().Name} on '{StepPrefix}' step executing '{StepText}'");
            }
            ExceptionDispatchInfo.Capture(e).Throw();
        }
        finally
        {
            EndTime = Options.Clock.UtcNow;
            TimeTaken = timer.Elapsed;
            Output = TestOutputData.Instance.ToString().Trim();
            try
            {
                var hookException = await RunAfterHooks(test, afterHooks);
                // Hooks are not interrupted, but their elapsed time still consumes
                // the scenario budget, including the final step's after hooks.
                if (executionException == null && remainingScenarioTime.HasValue && timer.Elapsed >= remainingScenarioTime.Value)
                {
                    var timeout = CreateTimeoutException("Scenario", scenarioTimeout ?? throw new InvalidOperationException("The scenario timeout is missing."));
                    SetExceptionResult(timeout);
                    throw timeout;
                }
                if (executionException == null && hookException != null)
                {
                    SetExceptionResult(hookException);
                    ExceptionDispatchInfo.Capture(hookException).Throw();
                }
            }
            finally
            {
                // Include hook output without charging after-hook work to the step duration.
                Output = TestOutputData.Instance.ToString().Trim();
                TestOutputData.ClearCurrentTaskData();
            }
        }
    }

    private async Task<Exception?> RunAfterHooks(BDTestBase? test, Func<StepResult, Task>? globalHooks)
    {
        Exception? firstException = null;
        if (test != null)
        {
            await Invoke(test.OnAfterStep);
        }
        if (globalHooks != null)
        {
            foreach (Func<StepResult, Task> hook in globalHooks.GetInvocationList())
            {
                await Invoke(hook);
            }
        }
        return firstException;

        async Task Invoke(Func<StepResult, Task> hook)
        {
            try
            {
                await hook(ResultSnapshot.Create(this));
            }
            catch (Exception e)
            {
                firstException ??= e;
                await Console.Out.WriteLineAsync($"After-step hook failed: {e}");
            }
        }
    }

    private void SetExceptionResult(Exception exception)
    {
        Status = ExceptionOutcomeClassifier.Classify(exception, Options, TimedOut);

        if (Status == Status.Failed || Status == Status.NotImplemented)
        {
            Exception = new ExceptionWrapper(exception);
        }
    }

    private async Task RunWithTimeout(TimeSpan remaining, string scope, TimeSpan configuredTimeout)
    {
        if (remaining <= TimeSpan.Zero)
        {
            throw CreateTimeoutException(scope, configuredTimeout);
        }

        var runnable = ExecutionRunnable;
        CancellationTokenSource? cancellation = new();
#if !NET8_0_OR_GREATER
        using var delayCancellation = new CancellationTokenSource();
#endif
        // A scheduling boundary also bounds synchronous steps and the synchronous
        // portion of an async method. Never let late completion mutate step results.
        var token = cancellation.Token;
        var execution = Task.Run(() => runnable.Run(token));
        try
        {
#if NET8_0_OR_GREATER
            // Carry user exceptions as values so a user's TimeoutException cannot
            // be mistaken for the timeout raised by WaitAsync.
            Exception? failure;
            try
            {
                failure = await CaptureFailure(execution).WaitAsync(remaining, Options.Clock.Provider).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                TimedOut = true;
                _ = CancelAndObserve(execution, cancellation);
                cancellation = null;
                throw CreateTimeoutException(scope, configuredTimeout);
            }
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
            return;
#else
            var delay = Task.Delay(remaining, delayCancellation.Token);
            if (await Task.WhenAny(execution, delay).ConfigureAwait(false) == execution)
            {
                await execution.ConfigureAwait(false);
                return;
            }
            TimedOut = true;
            _ = CancelAndObserve(execution, cancellation);
            cancellation = null;
            throw CreateTimeoutException(scope, configuredTimeout);
#endif
        }
        finally
        {
#if !NET8_0_OR_GREATER
            delayCancellation.Cancel();
#endif
            cancellation?.Dispose();
        }
    }

#if NET8_0_OR_GREATER
    private static async Task<Exception?> CaptureFailure(Task execution)
    {
        try
        {
            await execution.ConfigureAwait(false);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
#endif

    private TimeoutException CreateTimeoutException(string scope, TimeSpan configuredTimeout)
    {
        TimedOut = true;
        return new TimeoutException($"{scope} timed out after {configuredTimeout} while executing '{StepText}'.");
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
