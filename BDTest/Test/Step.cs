using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Threading;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Output;
using BDTest.Settings;
using BDTest.Test.Steps;
using Newtonsoft.Json;

namespace BDTest.Test;

public class Step
{
    [JsonIgnore]
    internal Runnable Runnable { get; private set; }
        
    [JsonProperty]
    public string Guid { get; private set; }
        
    [JsonProperty] 
    public string ReportId { get; private set; }
        
    [JsonProperty]
    public string ScenarioId { get; private set; }

    [JsonProperty]
    public DateTime StartTime { get; private set; }

    [JsonProperty]
    public DateTime EndTime { get; private set; }
        
    [JsonProperty]
    private StepType StepType { get; }
    private string StepPrefix => StepType.GetValue();

    [JsonProperty]
    public string Output { get; private set; }

    [JsonConverter(typeof(TimespanConverter))]
    [JsonProperty]
    public TimeSpan TimeTaken { get; private set; }

    [JsonProperty]
    public Status Status { get; internal set; } = Status.NotRun;

    [JsonProperty]
    public ExceptionWrapper Exception { get; private set; }

    /// <summary>The timeout for this step, or null for no step-specific limit.</summary>
    [JsonProperty]
    public TimeSpan? Timeout { get; internal set; }

    /// <summary>True when a step or scenario timeout failed this step.</summary>
    [JsonProperty]
    public bool TimedOut { get; private set; }

    /// <summary>Non-fatal errors encountered while rendering this attempt's step text.</summary>
    [JsonProperty]
    public List<string> Diagnostics { get; private set; } = new();

    private bool _shouldSkip;
    private readonly string _stepTextTemplate;
    private string _generatedStepText;

    internal Step(Runnable runnable, StepType stepType, string scenarioId, string reportId)
    {
        Guid = System.Guid.NewGuid().ToString("N");
        ScenarioId = scenarioId;
        Runnable = runnable;
        StepType = stepType;
        ReportId = reportId;
        // Construction and reporting retain placeholders without running user argument expressions.
        _stepTextTemplate = StepTextHelper.GetStepTextTemplate(Runnable.ExpressionBody, Runnable.AcceptsCancellationToken);
        _generatedStepText = _stepTextTemplate;
        SetStepText();
    }

    // Copy only the step definition. Execution results belong to one scenario.
    internal Step(Step source, string scenarioId)
    {
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
    }

    [JsonConstructor]
    private Step(StepType stepType)
    {
        StepType = stepType;
    }

    [JsonProperty]
    public string StepText { get; private set; }

    [JsonIgnore]
    internal Func<string> OverriddenStepText { get; set; }

    [JsonIgnore] internal Dictionary<string, string> ExampleValues { get; set; }

    private string SubstituteExampleValues(string text) => ExampleValues == null ? text
        : Regex.Replace(text, "<([^<>]+)>", match => ExampleValues.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);

    [JsonIgnore]
    public bool IsRetry { get; set; }

    internal void ReleaseRuntimeReferences()
    {
        Runnable = null;
        OverriddenStepText = null;
    }

    internal void ResetData()
    {
        _generatedStepText = _stepTextTemplate;
        StepText = SubstituteExampleValues($"{StepPrefix} {_stepTextTemplate}");
        Diagnostics.Clear();
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
        if (OverriddenStepText != null)
        {
            var invokedStepText = OverriddenStepText.Invoke();
            if (!string.IsNullOrWhiteSpace(invokedStepText))
            {
                StepText = SubstituteExampleValues($"{StepPrefix} {invokedStepText}");
                return;
            }
        }

        if (evaluateArguments)
        {
            _generatedStepText = StepTextHelper.GetStepText(Runnable.ExpressionBody, Runnable.AcceptsCancellationToken, Diagnostics.Add);
        }
        StepText = SubstituteExampleValues($"{StepPrefix} {_generatedStepText}");
    }

    private void SetShouldSkip()
    {
        _shouldSkip = false;
        var methodCallExpression = Runnable.ExpressionBody as MethodCallExpression;
        var methodInfo = methodCallExpression?.Method;

        var skipStepAttributes = (methodInfo?.GetCustomAttributes(
                                      typeof(SkipStepAttribute), true) ??
                                  new string[] { }) as SkipStepAttribute[];

        if (skipStepAttributes == null || !skipStepAttributes.Any())
        {
            return;
        }

        foreach (var skipStepAttributeOnStep in skipStepAttributes)
        {
            if (BDTestSettings.GlobalSkipStepRules.Rules.Any(skipStepRuleInSettings =>
                    skipStepRuleInSettings.AssociatedSkipAttributeType == skipStepAttributeOnStep.GetType()
                    && skipStepRuleInSettings.Condition(skipStepAttributeOnStep)))
            {
                _shouldSkip = true;
                break;   
            }
        }
    }

    internal async Task Execute(BDTestBase test, TimeSpan? scenarioTimeout = null, TimeSpan? remainingScenarioTime = null)
    {
        SetShouldSkip();

        // Keep both subscriptions stable for this step, including its after hooks.
        var beforeHooks = BDTestSettings.BeforeStepHooks;
        var afterHooks = BDTestSettings.AfterStepHooks;
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
                    await hook(this).ConfigureAwait(false);
                }
            }
            if (test != null)
            {
                await test.OnBeforeStep(this).ConfigureAwait(false);
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
                await RunWithTimeout(limit.Value, scenarioLimit ? "Scenario" : "Step", scenarioLimit ? scenarioTimeout.Value : Timeout.Value).ConfigureAwait(false);
            }
            else
            {
                await Runnable.Run().ConfigureAwait(false);
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
                await Console.Out.WriteLineAsync($"{Environment.NewLine}{e.GetType().Name} on '{StepPrefix}' step executing '{Runnable.GetExpressionString()}'").ConfigureAwait(false);
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
                var hookException = await RunAfterHooks(test, afterHooks).ConfigureAwait(false);
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
            await Invoke(test.OnAfterStep).ConfigureAwait(false);
        }
        if (globalHooks != null)
        {
            foreach (Func<Step, Task> hook in globalHooks.GetInvocationList())
            {
                await Invoke(hook).ConfigureAwait(false);
            }
        }
        return firstException;

        async Task Invoke(Func<Step, Task> hook)
        {
            try
            {
                await hook(this).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                firstException ??= e;
                await Console.Out.WriteLineAsync($"After-step hook failed: {e}").ConfigureAwait(false);
            }
        }
    }

    private void SetExceptionResult(Exception exception)
    {
        if (exception is NotImplementedException)
        {
            Status = Status.NotImplemented;
        }
        else if (!TimedOut && BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes.Contains(exception.GetType()))
        {
            Status = Status.Passed;
        }
        else if (!TimedOut && BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Contains(exception.GetType()))
        {
            Status = Status.Skipped;
        }
        else if (!TimedOut && BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Contains(exception.GetType()))
        {
            Status = Status.Inconclusive;
        }
        else
        {
            Status = Status.Failed;
        }

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
        if ((StepType == StepType.When || StepType == StepType.ButWhen) && BDTestSettings.Debug.ShouldSkipWhenStep)
        {
            return true;
        }

        return _shouldSkip;
    }
}
