using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using BDTest.Exceptions;
using BDTest.Extensions;
using BDTest.Helpers;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Settings;

namespace BDTest.Test;

/// <summary>A single-use outline with sequential, independently retried example rows.</summary>
public sealed class ScenarioOutlineBuilder<TExample> : BuildableTest
{
    private TExample[] _examples;
    private Func<TExample, RunnableStepBuilder> _scenario;
    private readonly Dictionary<string, string>[] _values;
    private int _executionStarted;

    internal ScenarioOutlineBuilder(string text, IEnumerable<TExample> examples,
        Func<TExample, RunnableStepBuilder> scenario, BDTestBase testBase, string testId,
        string callerMember, string callerFile)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("An outline needs scenario text.", nameof(text));
        if (examples == null) throw new ArgumentNullException(nameof(examples));
        _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        _examples = examples.ToArray();
        if (_examples.Length == 0) throw new ArgumentException("An outline needs at least one example.", nameof(examples));
        var properties = typeof(TExample).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetMethod?.IsPublic == true && property.GetIndexParameters().Length == 0).ToArray();
        _values = _examples.Select(example =>
        {
            if (example == null) throw new ArgumentException("Examples cannot contain null rows.", nameof(examples));
            return properties.ToDictionary(property => property.Name, property =>
            {
                var value = property.GetValue(example);
                return value == null ? "null" : Convert.ToString(value, CultureInfo.InvariantCulture);
            });
        }).ToArray();
        Guid = System.Guid.NewGuid().ToString("N");
        TestId = testId;
        BdTestBase = testBase;
        CallerMember = callerMember;
        CallerFile = callerFile;
        ReportId = TestHolder.CurrentReportId;
        StoryText = StoryTextHelper.GetStoryText(testBase);
        ScenarioText = new ScenarioText(text);
        CustomTestInformation = TestInformationAttributeHelper.GetTestInformationAttributes(this.GetTestMethod());
        TestHolder.NotRun[Guid] = this;
    }

    public Task<Scenario> BDTestAsync() => Invoke(false);
    public Scenario BDTest() => Invoke(true).GetAwaiter().GetResult();
    public TaskAwaiter<Scenario> GetAwaiter() => BDTestAsync().GetAwaiter();

    private Task<Scenario> Invoke(bool runOnThreadPool)
    {
        if (Interlocked.Exchange(ref _executionStarted, 1) != 0)
            throw new AlreadyExecutedException("This scenario outline has already started execution. Create a new outline to run more examples.");
        TestOutputData.TestId = Guid;
        TestOutputData.FrameworkExecutionId = TestId;
        var outline = new Scenario(new List<Step>(), this) { IsOutline = true };
        return runOnThreadPool ? Task.Run(() => ExecuteAsync(outline)) : ExecuteAsync(outline);
    }

    private static Status ClassifyOutcome(Exception exception)
    {
        if (exception is NotImplementedException) return Status.NotImplemented;
        var settings = BDTestSettings.CustomExceptionSettings;
        var exceptionType = exception.GetType();
        if (settings.SuccessExceptionTypes.Contains(exceptionType)) return Status.Passed;
        if (settings.SkipExceptionTypes.Contains(exceptionType)) return Status.Skipped;
        if (settings.InconclusiveExceptionTypes.Contains(exceptionType)) return Status.Inconclusive;
        return Status.Failed;
    }

    private async Task<Scenario> ExecuteAsync(Scenario outline)
    {
        outline.StartTime = DateTime.UtcNow;
        var failures = new List<(Status Status, ExceptionDispatchInfo Exception)>();
        var previousNotRun = new HashSet<string>(TestHolder.NotRun
            .Where(pair => pair.Value.TestId == TestId).Select(pair => pair.Key));
        try
        {
            for (var index = 0; index < _examples.Length; index++)
            {
                try
                {
                    BdTestBase.RecreateContextOnRetry();
                    var builder = _scenario(_examples[index]) ?? throw new InvalidOperationException("The example factory returned no scenario builder.");
                    if (builder.TestId != TestId || !ReferenceEquals(builder.BdTestBase, BdTestBase))
                        throw new InvalidOperationException("Example scenarios must be built by the outline's test instance.");
                    await builder.Invoke(builder, outline: outline, exampleValues: _values[index]).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    Scenario row;
                    // A factory or context error still gets an example row in the report.
                    if (outline.Examples.Count == index)
                    {
                        row = new Scenario(new List<Step>(), this, outline, _values[index])
                        {
                            StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow,
                            Status = ClassifyOutcome(exception), ExecutionException = new ExceptionWrapper(exception)
                        };
                        row.ReleaseRuntimeReferences();
                    }
                    else row = outline.Examples[index];
                    // Framework success exceptions must not fail the outline.
                    if (row.Status != Status.Passed)
                    {
                        if (row.Exception == null && row.Status != Status.Skipped)
                            row.ExecutionException = new ExceptionWrapper(exception);
                        failures.Add((row.Status, ExceptionDispatchInfo.Capture(exception)));
                    }
                }
            }

            outline.Status = outline.Examples.OrderBy(row => row.Status.GetOrder()).First().Status;
            if (outline.Status == Status.Skipped && outline.Examples.Any(row => row.Status == Status.Passed))
                outline.Status = Status.Passed;
            var failure = failures.FirstOrDefault(item => item.Status == outline.Status);
            failure.Exception?.Throw();
            if (outline.Status == Status.Skipped) BdTestBase.NotifyScenarioSkipped("All scenario outline examples were skipped.");
            return outline;
        }
        finally
        {
            outline.EndTime = DateTime.UtcNow;
            outline.TimeTaken = outline.EndTime - outline.StartTime;
            outline.Output = string.Join(Environment.NewLine, outline.Examples.Select(row => row.Output));
            TestHolder.AddScenario(outline);
            TestOutputData.TestId = outline.Guid;
            TestOutputData.FrameworkExecutionId = TestId;
            outline.ReleaseRuntimeReferences();
            _examples = null;
            _scenario = null;
            // Discard unfinished chains created by a row factory that threw or branched.
            foreach (var pending in TestHolder.NotRun.Where(pair => pair.Value.TestId == TestId && !previousNotRun.Contains(pair.Key)))
                TestHolder.NotRun.TryRemove(pending.Key, out _);
        }
    }
}
