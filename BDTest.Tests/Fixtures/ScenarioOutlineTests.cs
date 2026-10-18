using BDTest.Attributes;
using BDTest.Exceptions;
using BDTest.Maps;
using BDTest.Helpers;
using BDTest.Settings;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ScenarioOutlineTests : AbstractContextBDTestBase<ScenarioOutlineTests.RowContext>
{
    private readonly string _executionId = Guid.NewGuid().ToString();
    protected override string BDTestExecutionId => _executionId;
    private readonly List<RowContext> _contexts = new();
    private readonly List<int> _visited = new();
    private readonly Dictionary<int, int> _attempts = new();

    [OneTimeSetUp]
    public static void RegisterOutcomes()
    {
        BDTestSettings.CustomExceptionSettings.SkipExceptionTypes.Add(typeof(SkipRowException));
        BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes.Add(typeof(SuccessRowException));
        BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.Add(typeof(InconclusiveRowException));
        BDTestSettings.GlobalSkipTestRules.Add(scenario => scenario.BdTestBaseClass is SkipCapture
            && scenario.ExampleValues.TryGetValue("Skip", out var skip) && skip == "True");
        BDTestSettings.GlobalSkipTestRules.Add(scenario =>
        {
            if (scenario.ExampleValues.TryGetValue("ThrowSkipRule", out var value) && value == "True")
                throw new InvalidOperationException("<skip rule failed>");
            return false;
        });
    }

    private string _retryHook;
    private Exception _retryHookFailure;

    public override Task OnBeforeRetry()
    {
        if (_retryHook == "before") throw _retryHookFailure;
        return Task.CompletedTask;
    }

    [BDTestRetrySetUp]
    public void RetrySetup()
    {
        if (_retryHook == "setup") throw _retryHookFailure;
    }

    [BDTestRetryTearDown]
    public void RetryTeardown()
    {
        if (_retryHook == "teardown") throw _retryHookFailure;
    }

    [TestCase("before", false), TestCase("setup", false), TestCase("teardown", false)]
    [TestCase("before", true), TestCase("setup", true), TestCase("teardown", true), BDTestRetry(1)]
    public async Task Retry_hook_failures_fail_the_row_and_keep_diagnostics(string hook, bool nestedCause)
    {
        _retryHook = hook;
        _retryHookFailure = new InvalidOperationException("<retry hook failed>",
            nestedCause ? new Exception("nested cause") : null);
        await Assert.ThrowsAsync<ErrorOccurredDuringRetryActionException>(async () => await ScenarioOutline("Retry hook error",
            new[] { new { Value = 1 }, new { Value = 2 } },
            row => Given(() => RetryRow(row.Value)).When(() => NoOp()).Then(() => NoOp())));
        var outline = BDTestUtil.GetScenarios().Single(x => x.FrameworkTestId == _executionId);
        Assert.That(outline.Status, Is.EqualTo(Status.Failed));
        Assert.That(outline.Examples.Select(row => row.Status), Is.EqualTo(new[] { Status.Failed, Status.Passed }));
        Assert.That(outline.Examples[0].RetryCount, Is.EqualTo(1));
        foreach (var result in new[] { outline, ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(outline)) })
        {
            var failedRow = result.Examples[0];
            Assert.That(failedRow.Attempts, Has.Count.EqualTo(2));
            Assert.That(failedRow.Attempts.Last().Exception.Message, Is.EqualTo("<retry hook failed>"));
            Assert.That(failedRow.Exception.Message, Is.EqualTo(_retryHookFailure.Message));
            Assert.That(failedRow.Exception.AsString, Is.EqualTo(_retryHookFailure.ToString()));
            Assert.That(failedRow.EndTime, Is.EqualTo(failedRow.Attempts.Last().EndTime));
            Assert.That(failedRow.TimeTaken, Is.GreaterThan(TimeSpan.Zero));
            Assert.That(failedRow.TimeTaken, Is.EqualTo(failedRow.Attempts.Last().TimeTaken));
            var razor = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioExamples.cshtml", result);
            Assert.That(razor, Does.Contain($"<td>{failedRow.TimeTaken}</td>"));
        }
        Assert.That(BDTestHtmlHelper.GetTestHtmlReport(),
            Does.Contain($"title=\"{outline.Examples[0].TimeTaken}\">"));
        Assert.That(_visited, Is.EqualTo(new[] { 1, 2 }));
        await AssertRowErrorReported(outline, "&lt;retry hook failed&gt;");
        if (nestedCause)
            await AssertRowErrorReported(outline, "nested cause");
    }

    [Test]
    public async Task Skip_rule_errors_with_unexecuted_steps_are_reported()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await ScenarioOutline("Skip rule error",
            new[] { new { Value = 1, ThrowSkipRule = true }, new { Value = 2, ThrowSkipRule = false } },
            row => Given(() => Record(row.Value)).When(() => NoOp()).Then(() => NoOp())));
        var outline = BDTestUtil.GetScenarios().Single(x => x.FrameworkTestId == _executionId);
        Assert.That(outline.Examples.Select(row => row.Status), Is.EqualTo(new[] { Status.Failed, Status.Passed }));
        Assert.That(outline.Examples[0].Steps.All(step => step.Exception == null), Is.True);
        Assert.That(_visited, Is.EqualTo(new[] { 2 }));
        await AssertRowErrorReported(outline, "&lt;skip rule failed&gt;");
    }

    private static async Task AssertRowErrorReported(Scenario outline, string encodedError)
    {
        foreach (var result in new[] { outline, ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(outline)) })
        {
            Assert.That(result.Examples[0].Exception, Is.Not.Null);
            var razor = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioExamples.cshtml", result);
            Assert.That(razor, Does.Contain(encodedError));
        }
        var standalone = System.Text.RegularExpressions.Regex.Match(BDTestHtmlHelper.GetTestHtmlReport(),
            $"<table class=\"examples\" id=\"examples-{outline.Guid}\">.*?</table>",
            System.Text.RegularExpressions.RegexOptions.Singleline).Value;
        Assert.That(standalone, Does.Contain(encodedError));
    }

    [TestCase("success"), TestCase("skip"), TestCase("inconclusive")]
    [NonParallelizable]
    public async Task Timeouts_fail_examples_even_when_timeout_exceptions_have_custom_outcomes(string outcome)
    {
        var settings = BDTestSettings.CustomExceptionSettings;
        var exceptionTypes = outcome switch
        {
            "success" => settings.SuccessExceptionTypes,
            "skip" => settings.SkipExceptionTypes,
            _ => settings.InconclusiveExceptionTypes
        };
        var previousTypes = exceptionTypes.ToArray();
        exceptionTypes.Add(typeof(TimeoutException));
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(async () => await ScenarioOutline("Timeout outcomes",
                new[] { new { Value = 1 }, new { Value = 2 } },
                row => Given(token => TimeoutRow(row.Value, token)).WithStepTimeout(TimeSpan.FromMilliseconds(150))
                    .When(() => NoOp()).Then(() => NoOp())).BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10)));
            var outline = BDTestUtil.GetScenarios().Single(x => x.FrameworkTestId == _executionId);
            Assert.That(outline.Status, Is.EqualTo(Status.Failed));
            Assert.That(outline.Examples.Select(row => row.Status), Is.EqualTo(new[] { Status.Failed, Status.Passed }));
            Assert.That(outline.Examples[0].TimedOut, Is.True);
            Assert.That(outline.Examples[0].Steps[0].Status, Is.EqualTo(Status.Failed));
            Assert.That(_visited, Is.EqualTo(new[] { 1, 2 }));
            await AssertRowErrorReported(outline, "timed out");
        }
        finally
        {
            exceptionTypes.Clear();
            foreach (var type in previousTypes) exceptionTypes.Add(type);
        }
    }

    private async Task TimeoutRow(int value, CancellationToken token)
    {
        _visited.Add(value);
        if (value == 1) await Task.Delay(System.Threading.Timeout.Infinite, token);
    }

    [Test, BDTestTag("outline")]
    public async Task Examples_share_one_outline_with_substituted_text_and_fresh_contexts()
    {
        var outline = await ScenarioOutline("Adding values", new[] { new { Value = 1 }, new { Value = 2 } },
            row => Given(() => Record(row.Value)).WithStepText(() => "value <Value>")
                .When(() => NoOp()).Then(() => NoOp()));

        Assert.That(outline.Status, Is.EqualTo(Status.Passed));
        Assert.That(outline.Tags, Does.Contain("outline"));
        Assert.That(outline.Examples.All(row => row.Tags.SequenceEqual(outline.Tags)), Is.True);
        Assert.That(outline.Examples, Has.Count.EqualTo(2));
        Assert.That(outline.Examples.Select(row => row.Steps[0].StepText), Is.EqualTo(new[] { "Given value 1", "Given value 2" }));
        Assert.That(outline.Examples[1].ExampleValues["Value"], Is.EqualTo("2"));
        Assert.That(_visited, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(_contexts.Distinct().Count(), Is.EqualTo(2));
        Assert.That(outline.Examples[0].Output, Does.Contain("row output 1").And.Not.Contain("row output 2"));
        Assert.That(outline.Examples[1].Output, Does.Contain("row output 2").And.Not.Contain("row output 1"));
        Assert.That(outline.Examples.All(row => row.Steps.All(step => step.ScenarioId == row.Guid)), Is.True);
        Assert.That(BDTestUtil.GetScenarios().Where(x => x.FrameworkTestId == _executionId), Is.EqualTo(new[] { outline }));
        Assert.That(BDTestUtil.GetNotRunScenarios().Where(x => x.TestId == _executionId), Is.Empty);
        var restored = ReportJson.DeserializeObject<BDTestOutputModel>(BDTestJsonHelper.GetTestJsonData());
        var restoredOutline = restored.Scenarios.Single(scenario => scenario.FrameworkTestId == _executionId);
        Assert.That(restoredOutline.Tags, Is.EqualTo(outline.Tags));
        Assert.That(restoredOutline.Examples.All(row => row.Tags.SequenceEqual(outline.Tags)), Is.True);
        Assert.That(restoredOutline.Examples[1].Steps[0].StepText, Is.EqualTo("Given value 2"));
        Assert.That(restored.Scenarios.Any(scenario => scenario.OutlineId == outline.Guid), Is.False);
    }

    [Test]
    public async Task Failed_row_does_not_prevent_later_examples_and_fails_the_outline()
    {
        var builder = ScenarioOutline("Failures", new[] { new { Value = 1 }, new { Value = 2 } },
            row => Given(() => RecordAndFail(row.Value)).When(() => NoOp()).Then(() => NoOp()));

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await builder);
        var outline = BDTestUtil.GetScenarios().Single(x => x.FrameworkTestId == _executionId);
        Assert.That(_visited, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(outline.Examples.Select(row => row.Status), Is.EqualTo(new[] { Status.Failed, Status.Passed }));
        Assert.That(outline.Status, Is.EqualTo(Status.Failed));
        Assert.That(outline.Exception, Is.Not.Null);
        Assert.That(outline.EndTime, Is.GreaterThanOrEqualTo(outline.StartTime));
        var failedRow = outline.Examples[0];
        Assert.That(failedRow.EndTime, Is.EqualTo(failedRow.Attempts.Single().EndTime));
        Assert.That(failedRow.TimeTaken, Is.EqualTo(failedRow.Attempts.Single().TimeTaken));
        await Assert.ThrowsAsync<AlreadyExecutedException>(async () => await builder.BDTestAsync());
    }

    [Test]
    public void Empty_examples_are_rejected_before_execution()
    {
        Assert.Throws<ArgumentException>(() => ScenarioOutline("Empty", Array.Empty<object>(),
            row => Given(() => NoOp()).When(() => NoOp()).Then(() => NoOp())));
    }

    [TestCase("passed", "skipped", Status.Passed)]
    [TestCase("skipped", "skipped", Status.Skipped)]
    [TestCase("success", "passed", Status.Passed)]
    [TestCase("inconclusive", "passed", Status.Inconclusive)]
    [TestCase("not-implemented", "passed", Status.NotImplemented)]
    [TestCase("skipped", "failed", Status.Failed)]
    public async Task Row_outcomes_determine_the_runner_result(string first, string second, Status expected)
    {
        var builder = ScenarioOutline("Outcomes", new[] { new { Outcome = first }, new { Outcome = second } },
            row => Given(() => Outcome(row.Outcome)).When(() => NoOp()).Then(() => NoOp()));
        Exception thrown = null;
        try { await builder; }
        catch (Exception exception) { thrown = exception; }
        var outline = BDTestUtil.GetScenarios().Single(x => x.FrameworkTestId == _executionId);
        Assert.That(outline.Status, Is.EqualTo(expected));
        Assert.That(outline.Examples, Has.Count.EqualTo(2));
        if (expected == Status.Passed) Assert.That(thrown, Is.Null);
        else Assert.That(thrown, Is.Not.Null);
    }

    [Test, BDTestRetry(1)]
    public async Task Retries_are_per_example_and_reset_its_context()
    {
        var amendmentScenarios = new List<Scenario>();
        ContextAmendment = context => amendmentScenarios.Add(context.CurrentScenario);
        var outline = await ScenarioOutline("Retries", new[] { new { Value = 1 }, new { Value = 2 } },
            row => Given(() => RetryRow(row.Value)).When(() => NoOp()).Then(() => NoOp()));
        Assert.That(outline.Examples.Select(row => row.RetryCount), Is.EqualTo(new[] { 1, 0 }));
        Assert.That(outline.Examples.All(row => row.Status == Status.Passed), Is.True);
        Assert.That(_contexts.Distinct().Count(), Is.EqualTo(3));
        Assert.That(amendmentScenarios, Is.EqualTo(new Scenario[] { null, outline.Examples[0], null }));
        var html = BDTestHtmlHelper.GetTestHtmlReport();
        var table = System.Text.RegularExpressions.Regex.Match(html,
            $"<table class=\"examples\" id=\"examples-{outline.Guid}\">.*?</table>",
            System.Text.RegularExpressions.RegexOptions.Singleline).Value;
        Assert.That(table, Does.Contain("<th>Retries</th>"));
        Assert.That(table, Does.Contain(">Passed</span></td><td class=\"mono\">1</td>"));
        Assert.That(table, Does.Contain(">Passed</span></td><td class=\"mono\">0</td>"));
        foreach (var result in new[] { outline, ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(outline)) })
        {
            var razor = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioExamples.cshtml", result);
            Assert.That(razor, Does.Contain("Retry history"));
            Assert.That(razor, Does.Contain("retry first example"));
            Assert.That(System.Net.WebUtility.HtmlDecode(razor), Does.Contain(result.Examples[0].Attempts[0].Exception.StackTrace));
            Assert.That(System.Text.RegularExpressions.Regex.Matches(razor, "aria-label=\"Retry history\"").Count, Is.EqualTo(1));
        }
    }

    [TestCase(false, false), TestCase(false, true), TestCase(true, false), TestCase(true, true)]
    public async Task Explicit_construction_output_stays_with_its_example(bool duringContext, bool fail)
    {
        var contextCount = 0;
        ContextAmendment = _ =>
        {
            if (duringContext) WriteConstructionOutput(++contextCount, fail);
        };
        Exception thrown = null;
        try
        {
            await ScenarioOutline("Explicit construction output", new[] { new { Value = 1 }, new { Value = 2 }, new { Value = 3 } }, row =>
            {
                var builder = Given(() => NoOp()).When(() => NoOp()).Then(() => NoOp());
                if (!duringContext) WriteConstructionOutput(row.Value, fail);
                return builder;
            });
        }
        catch (Exception exception) { thrown = exception; }
        Assert.That(thrown, fail ? Is.TypeOf<InvalidOperationException>() : Is.Null);
        var outline = BDTestUtil.GetScenarios().Single(x => x.FrameworkTestId == _executionId);
        foreach (var result in new[] { outline, ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(outline)) })
        {
            Assert.That(result.TestStartupInformation, Is.Null.Or.Empty);
            Assert.That(result.TearDownOutput, Is.Null.Or.Empty);
            for (var index = 0; index < result.Examples.Count; index++)
            {
                var row = result.Examples[index];
                Assert.That(row.TestStartupInformation, Is.EqualTo($"<startup {index + 1}>{Environment.NewLine}"));
                Assert.That(row.TearDownOutput, Is.EqualTo($"<teardown {index + 1}>{Environment.NewLine}"));
                Assert.That(row.Output, Does.Not.Contain("<startup").And.Not.Contain("<teardown"));
                Assert.That(row.Status, Is.EqualTo(fail && index == 1 ? Status.Failed : Status.Passed));
            }
            var razor = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioExamples.cshtml", result);
            foreach (var value in Enumerable.Range(1, 3))
            {
                Assert.That(razor, Does.Contain($"&lt;startup {value}&gt;").And.Contain($"&lt;teardown {value}&gt;"));
            }
        }
        var standalone = System.Text.RegularExpressions.Regex.Match(BDTestHtmlHelper.GetTestHtmlReport(),
            $"<table class=\"examples\" id=\"examples-{outline.Guid}\">.*?</table>",
            System.Text.RegularExpressions.RegexOptions.Singleline).Value;
        foreach (var value in Enumerable.Range(1, 3))
            Assert.That(standalone, Does.Contain($"&lt;startup {value}&gt;").And.Contain($"&lt;teardown {value}&gt;"));
    }

    private void WriteConstructionOutput(int value, bool fail)
    {
        WriteStartupOutput($"<startup {value}>");
        WriteTearDownOutput($"<teardown {value}>");
        if (fail && value == 2) throw new InvalidOperationException("construction failed");
    }

    [Test, BDTestTag("factory-outline")]
    public async Task Factory_failure_is_reported_and_later_examples_still_run()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await ScenarioOutline("Factory error",
            new[] { new { Value = 1 }, new { Value = 2 } }, row => BuildRow(row.Value)));
        var outline = BDTestUtil.GetScenarios().Single(x => x.FrameworkTestId == _executionId);
        Assert.That(outline.Examples.Select(row => row.Status), Is.EqualTo(new[] { Status.Failed, Status.Passed }));
        Assert.That(outline.Examples[0].Exception.Message, Is.EqualTo("factory failed"));
        Assert.That(outline.Tags, Does.Contain("factory-outline"));
        Assert.That(outline.Examples.All(row => row.Tags.SequenceEqual(outline.Tags)), Is.True);
        Assert.That(outline.Examples.Select(row => row.Guid).Distinct().Count(), Is.EqualTo(2));
        Assert.That(BDTestUtil.GetNotRunScenarios().Where(x => x.TestId == _executionId), Is.Empty);
    }

    [Test]
    public async Task Factory_failure_keeps_inner_exception_details_in_the_html_report()
    {
        await Assert.ThrowsAsync<TypeInitializationException>(async () => await ScenarioOutline("Nested factory error",
            new[] { new { Value = 1 } }, row =>
            {
                throw new TypeInitializationException("ExampleFactory", new InvalidOperationException("<inner factory failure>"));
            }));
        var outline = BDTestUtil.GetScenarios().Single(x => x.FrameworkTestId == _executionId);
        var error = outline.Examples.Single().Exception;
        var html = BDTestHtmlHelper.GetTestHtmlReport();

        Assert.That(html, Does.Contain(System.Net.WebUtility.HtmlEncode(error.AsString)));
        Assert.That(html, Does.Contain("&lt;inner factory failure&gt;").And.Not.Contain("<inner factory failure>"));
    }

    [Test]
    public async Task Rejected_foreign_builder_is_removed_without_discarding_unrelated_pending_scenarios()
    {
        var foreign = new SkipCapture();
        var unrelated = foreign.Given(() => NoOp()).When(() => NoOp()).Then(() => NoOp());
        RunnableStepBuilder rejected = null;
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await ScenarioOutline("Foreign builder",
                new[] { new { Value = 1 }, new { Value = 2 } }, row => row.Value == 1
                    ? rejected = foreign.Given(() => NoOp()).When(() => NoOp()).Then(() => NoOp())
                    : Given(() => Record(row.Value)).When(() => NoOp()).Then(() => NoOp())));

            Assert.That(exception.Message, Is.EqualTo("Example scenarios must be built by the outline's test instance."));
            var outline = BDTestUtil.GetScenarios().Single(x => x.FrameworkTestId == _executionId);
            Assert.That(outline.Examples.Select(row => row.Status), Is.EqualTo(new[] { Status.Failed, Status.Passed }));
            Assert.That(_visited, Is.EqualTo(new[] { 2 }));
            var pending = BDTestUtil.GetNotRunScenarios().ToArray();
            Assert.That(pending, Does.Not.Contain(rejected));
            Assert.That(pending, Does.Contain(unrelated));
        }
        finally
        {
            TestHolder.NotRun.TryRemove(unrelated.Guid, out _);
            if (rejected != null) TestHolder.NotRun.TryRemove(rejected.Guid, out _);
        }
    }

    [TestCase("success", Status.Passed, false)]
    [TestCase("skipped", Status.Skipped, false)]
    [TestCase("inconclusive", Status.Inconclusive, false)]
    [TestCase("not-implemented", Status.NotImplemented, false)]
    [TestCase("success", Status.Passed, true)]
    [TestCase("skipped", Status.Skipped, true)]
    [TestCase("inconclusive", Status.Inconclusive, true)]
    [TestCase("not-implemented", Status.NotImplemented, true)]
    public async Task Factory_and_context_outcomes_preserve_framework_classification(string outcome, Status rowStatus, bool duringContext)
    {
        var contextCount = 0;
        if (duringContext)
            ContextAmendment = _ => { if (++contextCount == 1) Outcome(outcome); };
        Exception thrown = null;
        try
        {
            await ScenarioOutline("Factory outcomes", new[] { new { Value = 1 }, new { Value = 2 } }, row =>
            {
                if (!duringContext && row.Value == 1) Outcome(outcome);
                return Given(() => Record(row.Value)).When(() => NoOp()).Then(() => NoOp());
            });
        }
        catch (Exception exception) { thrown = exception; }

        var outline = BDTestUtil.GetScenarios().Single(x => x.FrameworkTestId == _executionId);
        Assert.That(outline.Examples.Select(row => row.Status), Is.EqualTo(new[] { rowStatus, Status.Passed }));
        Assert.That(_visited, Is.EqualTo(new[] { 2 }));
        var expected = rowStatus == Status.Skipped ? Status.Passed : rowStatus;
        Assert.That(outline.Status, Is.EqualTo(expected));
        if (expected == Status.Passed) Assert.That(thrown, Is.Null);
        else Assert.That(thrown, Is.Not.Null);
        var row = outline.Examples[0];
        if (rowStatus is Status.Passed or Status.Skipped or Status.Inconclusive)
        {
            Assert.That(row.Exception, Is.Null);
            Assert.That(outline.Exception, Is.Null);
            var restored = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(outline));
            Assert.That(restored.Examples[0].Exception, Is.Null);
            Assert.That(restored.Exception, Is.Null);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Construction_does_not_expose_an_outline_or_previous_example(bool fail)
    {
        var amendmentScenarios = new List<Scenario>();
        var factoryScenarios = new List<Scenario>();
        var stepScenarios = new List<Scenario>();
        ContextAmendment = context => amendmentScenarios.Add(context.CurrentScenario);
        Exception thrown = null;
        try
        {
            await ScenarioOutline("Current example", new[] { new { Value = 1 }, new { Value = 2 }, new { Value = 3 } }, row =>
            {
                factoryScenarios.Add(BDTestContext.CurrentScenario);
                if (fail && row.Value == 2) throw new InvalidOperationException("factory failed");
                return Given(() => stepScenarios.Add(BDTestContext.CurrentScenario)).When(() => NoOp()).Then(() => NoOp());
            });
        }
        catch (Exception exception) { thrown = exception; }

        Assert.That(thrown, fail ? Is.TypeOf<InvalidOperationException>() : Is.Null);
        Assert.That(amendmentScenarios, Has.Count.EqualTo(3));
        Assert.That(amendmentScenarios, Is.All.Null);
        Assert.That(factoryScenarios, Has.Count.EqualTo(3));
        Assert.That(factoryScenarios, Is.All.Null);
        var outline = BDTestUtil.GetScenarios().Single(x => x.FrameworkTestId == _executionId);
        Assert.That(stepScenarios, Is.EqualTo(outline.Examples.Where(row => row.Status == Status.Passed)));
        Assert.That(BDTestContext.CurrentScenario, Is.SameAs(outline));
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task Construction_output_belongs_to_its_example_even_when_construction_fails(bool duringContext, bool fail)
    {
        var contextCount = 0;
        ContextAmendment = _ =>
        {
            var value = ++contextCount;
            Console.WriteLine($"context output {value} {_executionId}");
            ScenarioHtmlWriter.Text($"context markup {value}");
            if (duringContext && fail && value == 2) throw new InvalidOperationException("context failed");
        };
        Exception thrown = null;
        try
        {
            await ScenarioOutline("Construction output", new[] { new { Value = 1 }, new { Value = 2 }, new { Value = 3 } }, row =>
            {
                Console.WriteLine($"factory output {row.Value}");
                ScenarioHtmlWriter.Text($"factory markup {row.Value}");
                var builder = Given(() => Record(row.Value)).When(() => ScenarioHtmlWriter.Text($"step markup {row.Value}")).Then(() => NoOp());
                ScenarioHtmlWriter.Text($"built markup {row.Value}");
                Console.WriteLine($"built output {row.Value}");
                if (!duringContext && fail && row.Value == 2) throw new InvalidOperationException("factory failed");
                return builder;
            });
        }
        catch (Exception exception) { thrown = exception; }

        Assert.That(thrown, fail ? Is.TypeOf<InvalidOperationException>() : Is.Null);
        var outline = BDTestUtil.GetScenarios().Single(x => x.FrameworkTestId == _executionId);
        var restored = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(outline));
        foreach (var result in new[] { outline, restored })
        {
            Assert.That(result.Examples, Has.Count.EqualTo(3));
            for (var index = 0; index < result.Examples.Count; index++)
            {
                var row = result.Examples[index];
                var value = index + 1;
                Assert.That(row.Output, Does.Contain($"context output {value}"));
                Assert.That(row.CustomHtmlOutputForReport, Does.StartWith($"<span>context markup {value}</span><br>"));
                if (!(duringContext && fail && value == 2))
                {
                    Assert.That(row.Output, Does.Contain($"factory output {value}"));
                    Assert.That(row.Output, Does.Contain($"built output {value}"));
                    Assert.That(row.CustomHtmlOutputForReport, Does.Contain($"<span>factory markup {value}</span><br><span>built markup {value}</span><br>"));
                }
                foreach (var other in Enumerable.Range(1, 3).Where(other => other != value))
                {
                    Assert.That(row.Output, Does.Not.Contain($"output {other}"));
                    Assert.That(row.CustomHtmlOutputForReport, Does.Not.Contain($"markup {other}"));
                }
                if (!fail || value != 2)
                    Assert.That(row.CustomHtmlOutputForReport, Does.EndWith($"<span>step markup {value}</span><br>"));
                Assert.That(row.TestStartupInformation, Is.Null.Or.Empty);
                Assert.That(row.TearDownOutput, Is.Null.Or.Empty);
            }
            Assert.That(result.CustomHtmlOutputForReport, Is.Null.Or.Empty);
            Assert.That(result.Output, Does.Contain("context output 1").And.Contain("context output 2").And.Contain("context output 3"));
        }
        var razor = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioExamples.cshtml", restored);
        var fullStandalone = BDTestHtmlHelper.GetTestHtmlReport();
        foreach (var value in Enumerable.Range(1, 3))
            Assert.That(System.Text.RegularExpressions.Regex.Matches(fullStandalone, $"context output {value} {_executionId}").Count, Is.EqualTo(1));
        var standalone = System.Text.RegularExpressions.Regex.Match(fullStandalone,
            $"<table class=\"examples\" id=\"examples-{outline.Guid}\">.*?</table>",
            System.Text.RegularExpressions.RegexOptions.Singleline).Value;
        foreach (var html in new[] { razor, standalone })
            Assert.That(html, Does.Contain("context output 1").And.Contain("context output 2").And.Contain("context output 3"));
    }

    private RunnableStepBuilder BuildRow(int value)
    {
        var builder = Given(() => Record(value)).When(() => NoOp()).Then(() => NoOp());
        if (value == 1) throw new InvalidOperationException("factory failed");
        return builder;
    }

    [TestCase(false, Status.Passed, 0)]
    [TestCase(true, Status.Skipped, 1)]
    public async Task Rule_skips_substitute_row_text_and_notify_the_adapter_once(bool skipBoth, Status status, int notifications)
    {
        var capture = new SkipCapture();
        var outline = await capture.ScenarioOutline("Rule skips", new[] { new { Skip = true }, new { Skip = skipBoth } },
            row => capture.Given(() => NoOp()).WithStepText(() => "skip <Skip>").When(() => NoOp()).Then(() => NoOp()));
        Assert.That(outline.Status, Is.EqualTo(status));
        Assert.That(outline.Examples[0].Steps[0].StepText, Is.EqualTo("Given skip True"));
        Assert.That(capture.Notifications, Is.EqualTo(notifications));
    }

    [Test]
    public async Task Concurrent_entry_points_reject_reuse_while_first_row_is_running()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = ScenarioOutline("Concurrent", new[] { new { Value = 1 } },
            row => Given(() => Hold(entered, finish.Task)).When(() => NoOp()).Then(() => NoOp()));
        var execution = builder.BDTestAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await Assert.ThrowsAsync<AlreadyExecutedException>(async () => await builder.BDTestAsync());
            await Assert.ThrowsAsync<AlreadyExecutedException>(async () => await builder);
        }
        finally { finish.TrySetResult(); }
        Assert.That((await execution).Examples, Has.Count.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task All_entry_points_share_a_single_execution_guard(bool directAwait)
    {
        var builder = ScenarioOutline("Once", new[] { new { Value = 1 } },
            row => Given(() => Record(row.Value)).When(() => NoOp()).Then(() => NoOp()));
        var outline = directAwait ? await builder : await builder.BDTestAsync();
        await Assert.ThrowsAsync<AlreadyExecutedException>(async () => await builder.BDTestAsync());
        await Assert.ThrowsAsync<AlreadyExecutedException>(async () => await builder);
        Assert.That(outline.Examples, Has.Count.EqualTo(1));
        WriteTearDownOutput("outline teardown");
        Assert.That(outline.TearDownOutput, Does.Contain("outline teardown"));
        Assert.That(outline.Examples[0].TearDownOutput, Is.Null.Or.Empty);
    }

    [Test]
    public async Task Text_substitution_is_invariant_and_does_not_expand_values_recursively()
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            var outline = await ScenarioOutline("Text", new[] { new { Number = 1.5m, Text = "<Number>", Empty = (string)null } },
                row => Given(() => Template()).When(() => NoOp()).Then(() => NoOp()));
            Assert.That(outline.Examples[0].Steps[0].StepText, Is.EqualTo("Given 1.5 <Number> null <Unknown>"));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
    }

    [Test]
    public async Task Html_and_Razor_reports_show_encoded_examples_under_one_outline()
    {
        var outline = await ScenarioOutline("Report outline", new[] { new { Text = "<script>row</script>" }, new { Text = "second" } },
            row => Given(() => NoOp()).WithStepText(() => "value <Text>").When(() => NoOp()).Then(() => NoOp()));
        var restored = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(outline));
        var standalone = BDTestHtmlHelper.GetTestHtmlReport();
        var exampleTable = System.Text.RegularExpressions.Regex.Match(standalone,
            $"<table class=\"examples\" id=\"examples-{outline.Guid}\">.*?</table>", System.Text.RegularExpressions.RegexOptions.Singleline).Value;
        Assert.That(exampleTable, Is.Not.Empty);
        foreach (var html in new[]
        {
            "Examples" + exampleTable,
            await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioExamples.cshtml", restored),
            await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioModalPopup.cshtml", new ScenarioModalViewModel { Scenario = restored }),
            await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Scenario.cshtml", restored)
        })
        {
            Assert.That(html, Does.Contain("Examples"));
            Assert.That(html, Does.Contain("&lt;script&gt;row&lt;/script&gt;"));
            Assert.That(html, Does.Not.Contain("<script>row</script>"));
            Assert.That(System.Text.RegularExpressions.Regex.Matches(html, "class=\"example-row\"").Count, Is.EqualTo(2));
            // The standalone report shows the step keyword in its own column.
            var text = System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", " "), @"\s+", " ");
            Assert.That(text, Does.Contain("Given value"));
        }
    }

    private void Record(int value)
    {
        Assert.That(Context.Value, Is.Zero);
        Context.Value = value;
        Assert.That(BDTestContext.CurrentScenario.OutlineId, Is.Not.Null);
        _contexts.Add(Context);
        _visited.Add(value);
        Console.WriteLine($"row output {value}");
    }

    private void RecordAndFail(int value)
    {
        Record(value);
        if (value == 1) throw new InvalidOperationException("first row failed");
    }

    private static void NoOp() { }
    private static async Task Hold(TaskCompletionSource entered, Task finish)
    {
        entered.SetResult();
        await finish;
    }
    [StepText("<Number> <Text> <Empty> <Unknown>")]
    private static void Template() { }
    private void RetryRow(int value)
    {
        Record(value);
        _attempts.TryGetValue(value, out var attempts);
        _attempts[value] = attempts + 1;
        if (value == 1 && attempts == 0) throw new InvalidOperationException("retry first example");
    }
    private static void Outcome(string outcome)
    {
        switch (outcome)
        {
            case "skipped": throw new SkipRowException();
            case "success": throw new SuccessRowException();
            case "inconclusive": throw new InconclusiveRowException();
            case "not-implemented": throw new NotImplementedException();
            case "failed": throw new InvalidOperationException();
        }
    }
    private class SkipRowException : Exception { }
    private class SuccessRowException : Exception { }
    private class InconclusiveRowException : Exception { }
    private class SkipCapture : BDTestBase
    {
        private readonly string _id = Guid.NewGuid().ToString();
        protected override string BDTestExecutionId => _id;
        public int Notifications { get; private set; }
        protected override void OnScenarioSkipped(string reason) => Notifications++;
    }
    public class RowContext { public int Value { get; set; } }
}
