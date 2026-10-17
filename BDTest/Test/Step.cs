using System.Diagnostics;
using System.Linq.Expressions;
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

    internal async Task Execute()
    {
        SetStepText();

        var stopwatch = Stopwatch.StartNew();
        try
        {
            StartTime = DateTime.UtcNow;

            if (ShouldSkip())
            {
                StepText = $"[Skipped] {StepText}";
                Status = Status.Skipped;
                return;
            }

            await Runnable.Run().ConfigureAwait(false);
            Status = Status.Passed;
        }
        catch (NotImplementedException e)
        {
            Status = Status.NotImplemented;
            Exception = new ExceptionWrapper(e);
            throw;
        }
        catch (Exception e) when (BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes.Contains(e.GetType()))
        {
            Status = Status.Passed;
            throw;
        }
        catch (Exception e) when (BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Contains(e.GetType()))
        {
            Status = Status.Skipped;
            throw;
        }
        catch (Exception e) when (BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Contains(e.GetType()))
        {
            Status = Status.Inconclusive;
            throw;
        }
        catch (Exception e)
        {
            Status = Status.Failed;
            Exception = new ExceptionWrapper(e);
            await Console.Out.WriteLineAsync($"{Environment.NewLine}{e.GetType().Name} on '{StepPrefix}' step executing '{Runnable.GetExpressionString()}'").ConfigureAwait(false);
            throw;
        }
        finally
        {
            EndTime = DateTime.UtcNow;
            stopwatch.Stop();
            TimeTaken = stopwatch.Elapsed;
            Output = TestOutputData.Instance.ToString().Trim();
            TestOutputData.ClearCurrentTaskData();
        }
    }

    private bool ShouldSkip()
    {
        if (StepType == StepType.When && BDTestSettings.Debug.ShouldSkipWhenStep)
        {
            return true;
        }

        return _shouldSkip;
    }
}
