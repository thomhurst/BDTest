using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Threading;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Output;
using BDTest.Settings;
using BDTest.Test.Steps;
using System.Text.Json.Serialization;

namespace BDTest.Test;

public class Step
{
    [JsonIgnore]
    internal Runnable Runnable { get; private set; }
        
    [JsonInclude]
    public string Guid { get; internal set; }
        
    [JsonInclude]
    public string ReportId { get; internal set; }
        
    [JsonInclude]
    public string ScenarioId { get; internal set; }

    [JsonInclude]
    public DateTime StartTime { get; internal set; }

    [JsonInclude]
    public DateTime EndTime { get; internal set; }
        
    [JsonInclude]
    [JsonConverter(typeof(JsonStringEnumConverter<StepType>))]
    internal StepType StepType { get; }
    private string StepPrefix => StepType.GetValue();

    [JsonInclude]
    public string Output { get; internal set; }

    [JsonConverter(typeof(TimespanConverter))]
    [JsonInclude]
    public TimeSpan TimeTaken { get; internal set; }

    [JsonInclude]
    public Status Status { get; internal set; } = Status.NotRun;

    [JsonInclude]
    public ExceptionWrapper Exception { get; internal set; }

    /// <summary>The timeout for this step, or null for no step-specific limit.</summary>
    [JsonInclude]
    public TimeSpan? Timeout { get; internal set; }

    /// <summary>True when a step or scenario timeout failed this step.</summary>
    [JsonInclude]
    public bool TimedOut { get; internal set; }

    /// <summary>Non-fatal errors encountered while rendering this attempt's step text.</summary>
    [JsonInclude]
    public List<string> Diagnostics { get; internal set; } = new();

    /// <summary>Structured values captured for this execution attempt. Historical reports default to an empty list.</summary>
    [JsonInclude]
    public IReadOnlyList<StepArgument> Arguments
    {
        get => _arguments;
        internal set => _arguments = Array.AsReadOnly((value ?? Array.Empty<StepArgument>()).ToArray());
    }

    private IReadOnlyList<StepArgument> _arguments = Array.AsReadOnly(Array.Empty<StepArgument>());

    [JsonIgnore]
    internal List<Func<StepArgument>> AttachedArguments { get; } = new();

    /// <summary>Captured files from all attempts, distinguished by Attachment.AttemptNumber.</summary>
    [JsonInclude] public List<Attachment> Attachments { get; internal set; } = new();

    [JsonIgnore] private RunOptions Options { get; } = RunOptions.Legacy;

    private bool _shouldSkip;
    private string _stepTextTemplate;
    private string _generatedStepText;

    internal Step(Runnable runnable, StepType stepType, string scenarioId, string reportId, BDTestRun run = null)
    {
        Options = (run ?? BDTestRun.Default).Options;
        Guid = System.Guid.NewGuid().ToString("N");
        ScenarioId = scenarioId;
        Runnable = runnable;
        StepType = stepType;
        ReportId = reportId;
        // Construction and reporting retain placeholders without running user argument expressions.
        _stepTextTemplate = Runnable.Definition.GetText();
        _generatedStepText = _stepTextTemplate;
        SetStepText();
    }

    // Copy only the step definition. Execution results belong to one scenario.
    internal Step(Step source, string scenarioId)
    {
        Options = source.Options;
        Guid = System.Guid.NewGuid().ToString("N");
        ScenarioId = scenarioId;
        ReportId = source.ReportId;
        Runnable = source.Runnable;
        StepType = source.StepType;
        _stepTextTemplate = source._stepTextTemplate;
        _generatedStepText = _stepTextTemplate;
        StepText = $"{StepPrefix} {_stepTextTemplate}";
        OverriddenStepText = source.OverriddenStepText;
        Timeout = source.Timeout;
        _explicitArguments = source._explicitArguments;
        AttachedArguments.AddRange(source.AttachedArguments);
    }

    [JsonConstructor]
    internal Step(StepType stepType)
    {
        StepType = stepType;
    }

    [JsonInclude]
    public string StepText { get; internal set; }

    [JsonIgnore]
    internal Func<string> OverriddenStepText { get; set; }

    private IReadOnlyList<Func<object>> _explicitArguments;
    [JsonIgnore]
    internal IReadOnlyList<Func<object>> ExplicitArguments
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

    [JsonIgnore] internal Dictionary<string, string> ExampleValues { get; set; }

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
        Diagnostics.Clear();
        Arguments = Array.Empty<StepArgument>();
        TimedOut = false;
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
                _generatedStepText = Runnable.Definition.GetText(true, Diagnostics.Add, ExplicitArguments, Options.Converters, arguments.Add);
            }
            foreach (var reader in AttachedArguments)
            {
                StepTextHelper.GetArgumentValue(reader, "attachment", Diagnostics.Add, Options.Converters, arguments.Add);
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

    private void SetShouldSkip()
    {
        _shouldSkip = false;
        var methodInfo = Runnable.Definition.Method;

        var skipStepAttributes = (methodInfo?.GetCustomAttributes(
                                      typeof(SkipStepAttribute), true) ??
                                  new string[] { }) as SkipStepAttribute[];

        if (skipStepAttributes == null || !skipStepAttributes.Any())
        {
            return;
        }

        foreach (var skipStepAttributeOnStep in skipStepAttributes)
        {
            if (Options.SkipStepRules.Any(skipStepRuleInSettings =>
                    skipStepRuleInSettings.AssociatedSkipAttributeType == skipStepAttributeOnStep.GetType()
                    && skipStepRuleInSettings.Condition(skipStepAttributeOnStep)))
            {
                _shouldSkip = true;
                break;   
            }
        }
    }

    internal async Task Execute(BDTestBase test, TimeSpan? scenarioTimeout = null, TimeSpan? remainingScenarioTime = null,
        Scenario scenario = null)
    {
        using var attachmentScope = scenario == null ? null : new AttachmentScope(scenario, this);
        SetShouldSkip();

        // Keep both subscriptions stable for this step, including its after hooks.
        var beforeHooks = Options.BeforeStep;
        var afterHooks = Options.AfterStep;
        Exception executionException = null;
        var beforeHooksCompleted = false;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            StartTime = DateTime.UtcNow;
            Status = Status.Pending;

            if (beforeHooks != null)
            {
                foreach (Func<Step, Task> hook in beforeHooks.GetInvocationList())
                {
                    await hook(this);
                }
            }
            if (test != null)
            {
                await test.OnBeforeStep(this);
            }

            beforeHooksCompleted = true;

            if (ShouldSkip())
            {
                SetStepText();
                StepText = $"[Skipped] {StepText}";
                Status = Status.Skipped;
                return;
            }

            // Render once per attempt, after hooks and skip checks, immediately before invocation.
            SetStepText(evaluateArguments: true);

            var remaining = remainingScenarioTime - stopwatch.Elapsed;
            var scenarioLimit = remaining.HasValue && (!Timeout.HasValue || remaining.Value <= Timeout.Value);
            var limit = scenarioLimit ? remaining : Timeout;
            if (limit.HasValue)
            {
                await RunWithTimeout(limit.Value, scenarioLimit ? "Scenario" : "Step", scenarioLimit ? scenarioTimeout.Value : Timeout.Value);
            }
            else
            {
                await Runnable.Run();
            }
            Status = Status.Passed;
        }
        catch (Exception e)
        {
            // Preserve failures raised within budget even if after hooks later exceed it.
            if (!beforeHooksCompleted && remainingScenarioTime.HasValue && stopwatch.Elapsed >= remainingScenarioTime.Value)
            {
                e = CreateTimeoutException("Scenario", scenarioTimeout.Value);
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
            EndTime = DateTime.UtcNow;
            TimeTaken = stopwatch.Elapsed;
            Output = TestOutputData.Instance.ToString().Trim();
            try
            {
                var hookException = await RunAfterHooks(test, afterHooks);
                // Hooks are not interrupted, but their elapsed time still consumes
                // the scenario budget, including the final step's after hooks.
                if (executionException == null && remainingScenarioTime.HasValue && stopwatch.Elapsed >= remainingScenarioTime.Value)
                {
                    var timeout = CreateTimeoutException("Scenario", scenarioTimeout.Value);
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
                stopwatch.Stop();
                // Include hook output without charging after-hook work to the step duration.
                Output = TestOutputData.Instance.ToString().Trim();
                TestOutputData.ClearCurrentTaskData();
            }
        }
    }

    private async Task<Exception> RunAfterHooks(BDTestBase test, Func<Step, Task> globalHooks)
    {
        Exception firstException = null;
        if (test != null)
        {
            await Invoke(test.OnAfterStep);
        }
        if (globalHooks != null)
        {
            foreach (Func<Step, Task> hook in globalHooks.GetInvocationList())
            {
                await Invoke(hook);
            }
        }
        return firstException;

        async Task Invoke(Func<Step, Task> hook)
        {
            try
            {
                await hook(this);
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

        var runnable = Runnable;
        var cancellation = new CancellationTokenSource();
        using var delayCancellation = new CancellationTokenSource();
        // A scheduling boundary also bounds synchronous steps and the synchronous
        // portion of an async method. Never let late completion mutate step results.
        var token = cancellation.Token;
        var execution = Task.Run(() => runnable.Run(token));
        try
        {
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
        }
        finally
        {
            delayCancellation.Cancel();
            cancellation?.Dispose();
        }
    }

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

    private bool ShouldSkip()
    {
        if ((StepType == StepType.When || StepType == StepType.ButWhen) && Options.ShouldSkipWhenStep)
        {
            return true;
        }

        return _shouldSkip;
    }
}
