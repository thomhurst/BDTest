using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.ExceptionServices;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Output;
using BDTest.Settings;
using BDTest.Test.Steps;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

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

    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty]
    public Status Status { get; internal set; } = Status.Inconclusive;

    [JsonProperty]
    public ExceptionWrapper Exception { get; private set; }

    private bool _shouldSkip;
    private readonly string _generatedStepText;

    internal Step(Runnable runnable, StepType stepType, string scenarioId, string reportId)
    {
        Guid = System.Guid.NewGuid().ToString("N");
        ScenarioId = scenarioId;
        Runnable = runnable;
        StepType = stepType;
        ReportId = reportId;
        // Capture automatic text once; execution and reporting must not re-evaluate arguments.
        _generatedStepText = Runnable.Action != null
            ? StepTextHelper.GetStepText(Runnable.Action)
            : StepTextHelper.GetStepText(Runnable.Task);
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
        _generatedStepText = source._generatedStepText;
        StepText = source.StepText;
        OverriddenStepText = source.OverriddenStepText;
        _shouldSkip = source._shouldSkip;
    }

    [JsonConstructor]
    private Step()
    {
    }

    [JsonProperty]
    public string StepText { get; private set; }

    [JsonIgnore]
    internal Func<string> OverriddenStepText { get; set; }

    [JsonIgnore]
    public bool IsRetry { get; set; }

    internal void ReleaseRuntimeReferences()
    {
        Runnable = null;
        OverriddenStepText = null;
    }

    internal void ResetData()
    {
        Exception = null;
        Output = null;
        StartTime = DateTime.MinValue;
        EndTime = DateTime.MinValue;
        TimeTaken = TimeSpan.Zero;
        Status = Status.Inconclusive;
        IsRetry = true;
    }

    internal void SetStepText()
    {
        if (OverriddenStepText != null)
        {
            var invokedStepText = OverriddenStepText.Invoke();
            if (!string.IsNullOrWhiteSpace(invokedStepText))
            {
                StepText = $"{StepPrefix} {invokedStepText}";
                return;
            }
        }

        SetShouldSkip();
            
        StepText = $"{StepPrefix} {_generatedStepText}";
    }

    private void SetShouldSkip()
    {
        var methodCallExpression = (Runnable.Action?.Body ?? Runnable.Task.Body) as MethodCallExpression;
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

    internal async Task Execute(BDTestBase test)
    {
        SetStepText();

        // Keep both subscriptions stable for this step, including its after hooks.
        var beforeHooks = BDTestSettings.BeforeStepHooks;
        var afterHooks = BDTestSettings.AfterStepHooks;
        Exception executionException = null;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            StartTime = DateTime.UtcNow;

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

            if (ShouldSkip())
            {
                StepText = $"[Skipped] {StepText}";
                Status = Status.Skipped;
                return;
            }

            await Runnable.Run().ConfigureAwait(false);
            Status = Status.Passed;
        }
        catch (Exception e)
        {
            executionException = e;
            SetExceptionResult(e);
            if (Status == Status.Failed)
            {
                await Console.Out.WriteLineAsync($"{Environment.NewLine}{e.GetType().Name} on '{StepPrefix}' step executing '{Runnable.GetExpressionString()}'").ConfigureAwait(false);
            }
            throw;
        }
        finally
        {
            EndTime = DateTime.UtcNow;
            stopwatch.Stop();
            TimeTaken = stopwatch.Elapsed;
            Output = TestOutputData.Instance.ToString().Trim();
            try
            {
                var hookException = await RunAfterHooks(test, afterHooks).ConfigureAwait(false);
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
        else if (BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes.Contains(exception.GetType()))
        {
            Status = Status.Passed;
        }
        else if (BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Contains(exception.GetType()))
        {
            Status = Status.Skipped;
        }
        else if (BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Contains(exception.GetType()))
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

    private bool ShouldSkip()
    {
        if ((StepType == StepType.When || StepType == StepType.ButWhen) && BDTestSettings.Debug.ShouldSkipWhenStep)
        {
            return true;
        }

        return _shouldSkip;
    }
}
