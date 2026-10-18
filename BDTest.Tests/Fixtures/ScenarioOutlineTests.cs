using BDTest.Attributes;
using BDTest.Exceptions;
using BDTest.Maps;
using BDTest.Helpers;
using BDTest.Settings;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Test;
using Newtonsoft.Json;
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
    }

    [Test]
    public async Task Examples_share_one_outline_with_substituted_text_and_fresh_contexts()
    {
        var outline = await ScenarioOutline("Adding values", new[] { new { Value = 1 }, new { Value = 2 } },
            row => Given(() => Record(row.Value)).WithStepText(() => "value <Value>")
                .When(() => NoOp()).Then(() => NoOp()));

        Assert.That(outline.Status, Is.EqualTo(Status.Passed));
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
        var restored = JsonConvert.DeserializeObject<BDTestOutputModel>(BDTestJsonHelper.GetTestJsonData());
        var restoredOutline = restored.Scenarios.Single(scenario => scenario.FrameworkTestId == _executionId);
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
        var outline = await ScenarioOutline("Retries", new[] { new { Value = 1 }, new { Value = 2 } },
            row => Given(() => RetryRow(row.Value)).When(() => NoOp()).Then(() => NoOp()));
        Assert.That(outline.Examples.Select(row => row.RetryCount), Is.EqualTo(new[] { 1, 0 }));
        Assert.That(outline.Examples.All(row => row.Status == Status.Passed), Is.True);
        Assert.That(_contexts.Distinct().Count(), Is.EqualTo(3));
    }

    [Test]
    public async Task Factory_failure_is_reported_and_later_examples_still_run()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await ScenarioOutline("Factory error",
            new[] { new { Value = 1 }, new { Value = 2 } }, row => BuildRow(row.Value)));
        var outline = BDTestUtil.GetScenarios().Single(x => x.FrameworkTestId == _executionId);
        Assert.That(outline.Examples.Select(row => row.Status), Is.EqualTo(new[] { Status.Failed, Status.Passed }));
        Assert.That(outline.Examples[0].Exception.Message, Is.EqualTo("factory failed"));
        Assert.That(outline.Examples.Select(row => row.Guid).Distinct().Count(), Is.EqualTo(2));
        Assert.That(BDTestUtil.GetNotRunScenarios().Where(x => x.TestId == _executionId), Is.Empty);
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
            Assert.Throws<AlreadyExecutedException>(() => builder.BDTest());
            await Assert.ThrowsAsync<AlreadyExecutedException>(async () => await builder);
        }
        finally { finish.TrySetResult(); }
        Assert.That((await execution).Examples, Has.Count.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task All_entry_points_share_a_single_execution_guard(bool synchronous)
    {
        var builder = ScenarioOutline("Once", new[] { new { Value = 1 } },
            row => Given(() => Record(row.Value)).When(() => NoOp()).Then(() => NoOp()));
        var outline = synchronous ? builder.BDTest() : await builder.BDTestAsync();
        Assert.Throws<AlreadyExecutedException>(() => builder.BDTest());
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
        var restored = JsonConvert.DeserializeObject<Scenario>(JsonConvert.SerializeObject(outline));
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
            Assert.That(html, Does.Contain("Given value"));
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
