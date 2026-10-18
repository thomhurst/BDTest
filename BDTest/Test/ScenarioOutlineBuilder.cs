using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using BDTest.Exceptions;
using BDTest.Extensions;
using BDTest.Helpers;
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
        Run = testBase.TestRun;
        Run.ThrowIfDisposed();
        Guid = System.Guid.NewGuid().ToString("N");
        TestId = testId;
        BdTestBase = testBase;
        CallerMember = callerMember;
        CallerFile = callerFile;
        ReportId = Run.ReportId;
        StoryText = StoryTextHelper.GetStoryText(testBase);
        ScenarioText = new ScenarioText(text);
        var testMethod = this.GetTestMethod();
        CustomTestInformation = TestInformationAttributeHelper.GetTestInformationAttributes(testMethod);
        Tags = TagHelper.GetTags(this.GetTestClass(), testMethod);
        Run.Store.RegisterPending(this);
    }

    public Task<Scenario> BDTestAsync()
    {
        Run.ThrowIfDisposed();
        if (Interlocked.Exchange(ref _executionStarted, 1) != 0)
            throw new AlreadyExecutedException("This scenario outline has already started execution. Create a new outline to run more examples.");
        TestOutputData.Run = Run;
        Console.SetOut(TestOutputData.Instance);
        TestOutputData.TestId = Guid;
        TestOutputData.FrameworkExecutionId = TestId;
        var outline = new Scenario(new List<Step>(), this) { IsOutline = true };
        return ExecuteAsync(outline);
    }

    public TaskAwaiter<Scenario> GetAwaiter() => BDTestAsync().GetAwaiter();

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
        outline.Status = Status.Pending;
        var failures = new List<(Status Status, ExceptionDispatchInfo Exception)>();
        var previousNotRun = new HashSet<string>(Run.Store.NotRun
            .Where(pair => pair.Value.TestId == TestId).Select(pair => pair.Key));
        try
        {
            for (var index = 0; index < _examples.Length; index++)
            {
                var constructionOutput = new ScenarioConstructionOutput(TestId, Run);
                try
                {
                    RunnableStepBuilder builder;
                    var previousOutput = TestOutputData.ConstructionOutput;
                    try
                    {
                        TestOutputData.ConstructionOutput = constructionOutput;
                        BdTestBase.RecreateContextOnRetry();
                        builder = _scenario(_examples[index]) ?? throw new InvalidOperationException("The example factory returned no scenario builder.");
                        if (builder.TestId != TestId || !ReferenceEquals(builder.BdTestBase, BdTestBase))
                        {
                            builder.Run.Store.NotRun.TryRemove(builder.Guid, out _);
                            throw new InvalidOperationException("Example scenarios must be built by the outline's test instance.");
                        }
                    }
                    finally
                    {
                        TestOutputData.Run = Run;
                        TestOutputData.ConstructionOutput = previousOutput;
                    }
                    await builder.Invoke(builder, outline: outline, exampleValues: _values[index]);
                }
                catch (Exception exception)
                {
                    Scenario row;
                    // A factory or context error still gets an example row in the report.
                    if (outline.Examples.Count == index)
                    {
                        row = new Scenario(new List<Step>(), this, outline, _values[index])
                        {
                            StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow
                        };
                        row.ReleaseRuntimeReferences();
                    }
                    else row = outline.Examples[index];
                    if (row.EndTime == default)
                    {
                        row.EndTime = DateTime.UtcNow;
                        row.TimeTaken = row.EndTime - row.StartTime;
                    }
                    row.Status = row.TimedOut ? Status.Failed : ClassifyOutcome(exception);
                    // Framework success exceptions must not fail the outline.
                    if (row.Status != Status.Passed)
                    {
                        if (row.Exception == null && row.Status is Status.Failed or Status.NotImplemented)
                            row.ExecutionException = new ExceptionWrapper(exception is ErrorOccurredDuringRetryActionException
                                ? exception.GetBaseException() : exception);
                        failures.Add((row.Status, ExceptionDispatchInfo.Capture(exception)));
                    }
                }
                finally
                {
                    var row = outline.Examples[index];
                    if (!string.IsNullOrEmpty(constructionOutput.Startup.Value))
                        row.TestStartupInformation = constructionOutput.Startup.Value + row.TestStartupInformation;
                    if (!string.IsNullOrEmpty(constructionOutput.Teardown.Value))
                        row.TearDownOutput = constructionOutput.Teardown.Value + row.TearDownOutput;
                    var constructionHtml = constructionOutput.Html.Value;
                    if (!string.IsNullOrEmpty(constructionHtml))
                        row.CustomHtmlOutputForReport = constructionHtml + row.CustomHtmlOutputForReport;
                    row.Output = string.Join(Environment.NewLine,
                        new[] { constructionOutput.Text.Value, row.Output }.Where(output => !string.IsNullOrEmpty(output)));
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
            Run.Store.AddScenario(outline);
            TestOutputData.TestId = outline.Guid;
            TestOutputData.FrameworkExecutionId = TestId;
            outline.ReleaseRuntimeReferences();
            _examples = null;
            _scenario = null;
            // Discard unfinished chains created by a row factory that threw or branched.
            foreach (var pending in Run.Store.NotRun.Where(pair => pair.Value.TestId == TestId && !previousNotRun.Contains(pair.Key)))
                Run.Store.NotRun.TryRemove(pending.Key, out _);
        }
    }
}
