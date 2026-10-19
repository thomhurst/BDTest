using BDTest.Attributes;
using BDTest.Exceptions;
using BDTest.Maps;
using BDTest.Helpers;
using BDTest.Settings;
using BDTest.Razor.Reports.Models.ViewModels;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;
public class ScenarioOutlineTests : RunContextTestBase<ScenarioOutlineTests.RowContext>
{
    protected override bool BindCurrentTestMetadata => true;

    [After(HookType.Test)]
    public void FinishExecution() => EndTest();
    private Action<BDTestContext<RowContext>>? Configure { get; set; }
    protected override void ConfigureContext(BDTestContext<RowContext> context) => Configure?.Invoke(context);

    private readonly string _executionId = Guid.NewGuid().ToString();
    protected override string BDTestExecutionId => _executionId;
    private readonly List<RowContext> _contexts = new();
    private readonly List<int> _visited = new();
    private readonly Dictionary<int, int> _attempts = new();

    public ScenarioOutlineTests()
    {
        Options.SkipExceptionTypes.Add(typeof(SkipRowException));
        Options.SuccessExceptionTypes.Add(typeof(SuccessRowException));
        Options.InconclusiveExceptionTypes.Add(typeof(InconclusiveRowException));
        Options.SkipTestRules.Add(scenario => scenario.BdTestBaseClass is SkipCapture
            && Required(scenario.ExampleValues).TryGetValue("Skip", out var skip) && skip == "True");
        Options.SkipTestRules.Add(scenario =>
        {
            if (Required(scenario.ExampleValues).TryGetValue("ThrowSkipRule", out var value) && value == "True")
                throw new InvalidOperationException("<skip rule failed>");
            return false;
        });
    }

    private string? _retryHook;
    private Exception? _retryHookFailure;

    public override Task OnBeforeRetry()
    {
        if (_retryHook == "before") throw Required(_retryHookFailure);
        return Task.CompletedTask;
    }

    [BDTestRetrySetUp]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "TUnit0014", Justification = "BDTest discovers public attributed retry hooks by reflection.")]
    public void RetrySetup()
    {
        if (_retryHook == "setup") throw Required(_retryHookFailure);
    }

    [BDTestRetryTearDown]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "TUnit0014", Justification = "BDTest discovers public attributed retry hooks by reflection.")]
    public void RetryTeardown()
    {
        if (_retryHook == "teardown") throw Required(_retryHookFailure);
    }

    [Test]
    [Arguments("before", false), Arguments("setup", false), Arguments("teardown", false)]
    [Arguments("before", true), Arguments("setup", true), Arguments("teardown", true), BDTestRetry(1)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "TUnit0018", Justification = "TUnit creates a fresh fixture for each case; these fields configure that case's outline context and retry hooks.")]
    public async Task Retry_hook_failures_fail_the_row_and_keep_diagnostics(string hook, bool nestedCause)
    {
        _retryHook = hook;
        _retryHookFailure = new InvalidOperationException("<retry hook failed>",
            nestedCause ? new Exception("nested cause") : null);
        await Assert.That(async () => await ScenarioOutline("Retry hook error",
            new[] { new { Value = 1 }, new { Value = 2 } },
            row => Given(() => RetryRow(row.Value)).When(() => NoOp()).Then(() => NoOp()))).ThrowsExactly<ErrorOccurredDuringRetryActionException>();
        var outline = BDTestUtil.GetScenarios(TestRun).Single(x => x.FrameworkTestId == _executionId);
        await Assert.That(outline.Status).IsEqualTo(Status.Failed);
        await Assert.That(Required(outline.Examples).Select(row => row.Status)).IsEquivalentTo(new[] { Status.Failed, Status.Passed }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(outline.Examples[0].RetryCount).IsEqualTo(1);
        foreach (var result in new[] { outline, ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(outline)) })
        {
            var failedRow = Required(result.Examples)[0];
            await Assert.That(failedRow.Attempts).Count().IsEqualTo(2);
            await Assert.That(Required(Required(failedRow.Attempts).Last().Exception).Message).IsEqualTo("<retry hook failed>");
            await Assert.That(Required(failedRow.Attempts.Last().Exception).AsString).IsEqualTo(_retryHookFailure.ToString());
            await Assert.That(Required(failedRow.Exception).Message).IsEqualTo(_retryHookFailure.Message);
            await Assert.That(Required(failedRow.Exception).AsString).IsEqualTo(_retryHookFailure.ToString());
            await Assert.That(failedRow.EndTime).IsEqualTo(failedRow.Attempts.Last().EndTime);
            await Assert.That(failedRow.TimeTaken).IsGreaterThan(TimeSpan.Zero);
            await Assert.That(failedRow.TimeTaken).IsEqualTo(failedRow.Attempts.Last().TimeTaken);
            var razor = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioExamples.cshtml", result);
            await Assert.That(razor).Contains($"title=\"{failedRow.TimeTaken}\"");
        }
        await Assert.That(BDTestHtmlHelper.GetTestHtmlReport(null, TestRun)).Contains($"title=\"{outline.Examples[0].TimeTaken}\">");
        await Assert.That(_visited).IsEquivalentTo(new[] { 1, 2 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await AssertRowErrorReported(outline, "&lt;retry hook failed&gt;");
        if (nestedCause)
            await AssertRowErrorReported(outline, "nested cause");
    }

    [Test]
    public async Task Skip_rule_errors_with_unexecuted_steps_are_reported()
    {
        await Assert.That(async () => await ScenarioOutline("Skip rule error",
            new[] { new { Value = 1, ThrowSkipRule = true }, new { Value = 2, ThrowSkipRule = false } },
            row => Given(() => Record(row.Value)).When(() => NoOp()).Then(() => NoOp()))).ThrowsExactly<InvalidOperationException>();
        var outline = BDTestUtil.GetScenarios(TestRun).Single(x => x.FrameworkTestId == _executionId);
        await Assert.That(Required(outline.Examples).Select(row => row.Status)).IsEquivalentTo(new[] { Status.Failed, Status.Passed }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(outline.Examples[0].Steps).All(step => step.Exception == null)).IsTrue();
        await Assert.That(_visited).IsEquivalentTo(new[] { 2 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await AssertRowErrorReported(outline, "&lt;skip rule failed&gt;");
    }

    private async Task AssertRowErrorReported(Scenario outline, string encodedError)
    {
        foreach (var result in new[] { outline, ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(outline)) })
        {
            await Assert.That(Required(result.Examples)[0].Exception).IsNotNull();
            var razor = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioExamples.cshtml", result);
            await Assert.That(razor).Contains(encodedError);
        }
        var standalone = System.Text.RegularExpressions.Regex.Match(BDTestHtmlHelper.GetTestHtmlReport(null, TestRun),
            $"<table class=\"examples\" id=\"examples-{outline.Guid}\">.*?</table>",
            System.Text.RegularExpressions.RegexOptions.Singleline).Value;
        await Assert.That(standalone).Contains(encodedError);
    }

    [Test]
    [Arguments("success"), Arguments("skip"), Arguments("inconclusive")]
    [NotInParallel]
    public async Task Timeouts_fail_examples_even_when_timeout_exceptions_have_custom_outcomes(string outcome)
    {
        var settings = Options;
        var exceptionTypes = outcome switch
        {
            "success" => settings.SuccessExceptionTypes,
            "skip" => settings.SkipExceptionTypes,
            _ => settings.InconclusiveExceptionTypes
        };
        exceptionTypes.Add(typeof(TimeoutException));
        await Assert.That(async () => await ScenarioOutline("Timeout outcomes",
            new[] { new { Value = 1 }, new { Value = 2 } },
            row => Given(token => TimeoutRow(row.Value, token)).WithStepTimeout(TimeSpan.FromMilliseconds(150))
                .When(() => NoOp()).Then(() => NoOp())).BDTestAsync().WaitAsync(TimeSpan.FromSeconds(10))).ThrowsExactly<TimeoutException>();
        var outline = BDTestUtil.GetScenarios(TestRun).Single(x => x.FrameworkTestId == _executionId);
        await Assert.That(outline.Status).IsEqualTo(Status.Failed);
        await Assert.That(Required(outline.Examples).Select(row => row.Status)).IsEquivalentTo(new[] { Status.Failed, Status.Passed }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(outline.Examples[0].TimedOut).IsTrue();
        await Assert.That(Required(outline.Examples[0].Steps)[0].Status).IsEqualTo(Status.Failed);
        await Assert.That(_visited).IsEquivalentTo(new[] { 1, 2 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await AssertRowErrorReported(outline, "timed out");

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

        await Assert.That(outline.Status).IsEqualTo(Status.Passed);
        await Assert.That(outline.Tags).Contains("outline");
        await Assert.That(Required(outline.Examples).All(row => row.Tags.SequenceEqual(outline.Tags))).IsTrue();
        await Assert.That(outline.Examples).Count().IsEqualTo(2);
        await Assert.That(outline.Examples.Select(row => Required(row.Steps)[0].StepText)).IsEquivalentTo(new string?[] { "Given value 1", "Given value 2" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(outline.Examples[1].ExampleValues)["Value"]).IsEqualTo("2");
        await Assert.That(_visited).IsEquivalentTo(new[] { 1, 2 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(_contexts.Distinct().Count()).IsEqualTo(2);

        await Assert.That(outline.Examples[0].Output).Contains("row output 1").And.DoesNotContain("row output 2");

        await Assert.That(outline.Examples[1].Output).Contains("row output 2").And.DoesNotContain("row output 1");
        await Assert.That(outline.Examples.All(row => Required(row.Steps).All(step => step.ScenarioId == row.Guid))).IsTrue();
        await Assert.That(BDTestUtil.GetScenarios(TestRun).Where(x => x.FrameworkTestId == _executionId)).IsEquivalentTo(new[] { outline }, EqualityComparer<Scenario>.Default, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(BDTestUtil.GetNotRunScenarios(TestRun).Where(x => x.TestId == _executionId)).IsEmpty();
        var restored = ReportJson.DeserializeObject<BDTestOutputModel>(BDTestJsonHelper.GetTestJsonData(null, TestRun));
        var restoredOutline = Required(restored.Scenarios).Single(scenario => scenario.FrameworkTestId == _executionId);
        await Assert.That(Required(restoredOutline.Tags)).IsEquivalentTo(Required(outline.Tags), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(restoredOutline.Examples).All(row => row.Tags.SequenceEqual(outline.Tags))).IsTrue();
        await Assert.That(Required(restoredOutline.Examples[1].Steps)[0].StepText).IsEqualTo("Given value 2");
        await Assert.That(restored.Scenarios.Any(scenario => scenario.OutlineId == outline.Guid)).IsFalse();
    }

    [Test]
    public async Task Failed_row_does_not_prevent_later_examples_and_fails_the_outline()
    {
        var builder = ScenarioOutline("Failures", new[] { new { Value = 1 }, new { Value = 2 } },
            row => Given(() => RecordAndFail(row.Value)).When(() => NoOp()).Then(() => NoOp()));

        await Assert.That(async () => await builder).ThrowsExactly<InvalidOperationException>();
        var outline = BDTestUtil.GetScenarios(TestRun).Single(x => x.FrameworkTestId == _executionId);
        await Assert.That(_visited).IsEquivalentTo(new[] { 1, 2 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(outline.Examples).Select(row => row.Status)).IsEquivalentTo(new[] { Status.Failed, Status.Passed }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(outline.Status).IsEqualTo(Status.Failed);
        await Assert.That(outline.Exception).IsNotNull();
        await Assert.That(outline.EndTime).IsGreaterThanOrEqualTo(outline.StartTime);
        var failedRow = outline.Examples[0];
        await Assert.That(failedRow.EndTime).IsEqualTo(Required(failedRow.Attempts).Single().EndTime);
        await Assert.That(failedRow.TimeTaken).IsEqualTo(failedRow.Attempts.Single().TimeTaken);
        await Assert.That(async () => await builder.BDTestAsync()).ThrowsExactly<AlreadyExecutedException>();
    }

    [Test]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("BDTest", "BDT001", Justification = "An empty outline must throw during construction.")]
    public async Task Empty_examples_are_rejected_before_execution()
    {
        await Assert.That(() => ScenarioOutline("Empty", Array.Empty<object>(),
            row => Given(() => NoOp()).When(() => NoOp()).Then(() => NoOp()))).ThrowsExactly<ArgumentException>();
    }

    [Test]
    [Arguments("passed", "skipped", Status.Passed)]
    [Arguments("skipped", "skipped", Status.Skipped)]
    [Arguments("success", "passed", Status.Passed)]
    [Arguments("inconclusive", "passed", Status.Inconclusive)]
    [Arguments("not-implemented", "passed", Status.NotImplemented)]
    [Arguments("skipped", "failed", Status.Failed)]
    public async Task Row_outcomes_determine_the_runner_result(string first, string second, Status expected)
    {
        var builder = ScenarioOutline("Outcomes", new[] { new { Outcome = first }, new { Outcome = second } },
            row => Given(() => Outcome(row.Outcome)).When(() => NoOp()).Then(() => NoOp()));
        Exception? thrown = null;
        try { await builder; }
        catch (Exception exception) { thrown = exception; }
        var outline = BDTestUtil.GetScenarios(TestRun).Single(x => x.FrameworkTestId == _executionId);
        await Assert.That(outline.Status).IsEqualTo(expected);
        await Assert.That(outline.Examples).Count().IsEqualTo(2);
        if (expected == Status.Passed) await Assert.That(thrown).IsNull();
        else await Assert.That(thrown).IsNotNull();
    }

    [Test, BDTestRetry(1)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "TUnit0018", Justification = "TUnit creates a fresh fixture for each case; these fields configure that case's outline context and retry hooks.")]
    public async Task Retries_are_per_example_and_reset_its_context()
    {
        var amendmentScenarios = new List<Scenario?>();
        Configure = context => amendmentScenarios.Add(context.CurrentScenario);
        var outline = await ScenarioOutline("Retries", new[] { new { Value = 1 }, new { Value = 2 } },
            row => Given(() => RetryRow(row.Value)).When(() => NoOp()).Then(() => NoOp()));
        await Assert.That(Required(outline.Examples).Select(row => row.RetryCount)).IsEquivalentTo(new[] { 1, 0 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(outline.Examples.All(row => row.Status == Status.Passed)).IsTrue();
        await Assert.That(_contexts.Distinct().Count()).IsEqualTo(3);
        await Assert.That(amendmentScenarios).IsEquivalentTo(new Scenario?[] { null, outline.Examples[0], null }, EqualityComparer<Scenario?>.Default, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var html = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);
        var table = System.Text.RegularExpressions.Regex.Match(html,
            $"<table class=\"examples\" id=\"examples-{outline.Guid}\">.*?</table>",
            System.Text.RegularExpressions.RegexOptions.Singleline).Value;
        await Assert.That(table).Contains("<th>Retries</th>");
        await Assert.That(table).Contains(">Passed</span></td><td class=\"mono\">1</td>");
        await Assert.That(table).Contains(">Passed</span></td><td class=\"mono\">0</td>");
        foreach (var result in new[] { outline, ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(outline)) })
        {
            var razor = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioExamples.cshtml", result);
            await Assert.That(razor).Contains("Retry history");
            await Assert.That(razor).Contains("retry first example");
            await Assert.That(System.Net.WebUtility.HtmlDecode(razor)).Contains(Required(Required(Required(Required(result.Examples)[0].Attempts)[0].Exception).StackTrace));
            await Assert.That(System.Text.RegularExpressions.Regex.Matches(razor, "aria-label=\"Retry history\"").Count).IsEqualTo(1);
        }
    }

    [Test]
    [Arguments(false, false), Arguments(false, true), Arguments(true, false), Arguments(true, true)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "TUnit0018", Justification = "TUnit creates a fresh fixture for each case; these fields configure that case's outline context and retry hooks.")]
    public async Task Explicit_construction_output_stays_with_its_example(bool duringContext, bool fail)
    {
        var contextCount = 0;
        Configure = _ =>
        {
            if (duringContext) WriteConstructionOutput(++contextCount, fail);
        };
        Exception? thrown = null;
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

        if (fail) await Assert.That(thrown).IsTypeOf<InvalidOperationException>();
        else await Assert.That(thrown).IsNull();
        var outline = BDTestUtil.GetScenarios(TestRun).Single(x => x.FrameworkTestId == _executionId);
        foreach (var result in new[] { outline, ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(outline)) })
        {

            await Assert.That(result.TestStartupInformation).IsNullOrEmpty();

            await Assert.That(result.TearDownOutput).IsNullOrEmpty();
            for (var index = 0; index < Required(result.Examples).Count; index++)
            {
                var row = result.Examples[index];
                await Assert.That(row.TestStartupInformation).IsEqualTo($"<startup {index + 1}>{Environment.NewLine}");
                await Assert.That(row.TearDownOutput).IsEqualTo($"<teardown {index + 1}>{Environment.NewLine}");

                await Assert.That(row.Output).DoesNotContain("<startup").And.DoesNotContain("<teardown");
                await Assert.That(row.Status).IsEqualTo(fail && index == 1 ? Status.Failed : Status.Passed);
            }
            var razor = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioExamples.cshtml", result);
            foreach (var value in Enumerable.Range(1, 3))
            {

                await Assert.That(razor).Contains($"&lt;startup {value}&gt;").And.Contains($"&lt;teardown {value}&gt;");
            }
        }
        var standalone = System.Text.RegularExpressions.Regex.Match(BDTestHtmlHelper.GetTestHtmlReport(null, TestRun),
            $"<table class=\"examples\" id=\"examples-{outline.Guid}\">.*?</table>",
            System.Text.RegularExpressions.RegexOptions.Singleline).Value;
        foreach (var value in Enumerable.Range(1, 3))

            await Assert.That(standalone).Contains($"&lt;startup {value}&gt;").And.Contains($"&lt;teardown {value}&gt;");
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
        await Assert.That(async () => await ScenarioOutline("Factory error",
            new[] { new { Value = 1 }, new { Value = 2 } }, row => BuildRow(row.Value))).ThrowsExactly<InvalidOperationException>();
        var outline = BDTestUtil.GetScenarios(TestRun).Single(x => x.FrameworkTestId == _executionId);
        await Assert.That(Required(outline.Examples).Select(row => row.Status)).IsEquivalentTo(new[] { Status.Failed, Status.Passed }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(outline.Examples[0].Exception).Message).IsEqualTo("factory failed");
        await Assert.That(outline.Tags).Contains("factory-outline");
        await Assert.That(outline.Examples.All(row => row.Tags.SequenceEqual(outline.Tags))).IsTrue();
        await Assert.That(outline.Examples.Select(row => row.Guid).Distinct().Count()).IsEqualTo(2);
        await Assert.That(BDTestUtil.GetNotRunScenarios(TestRun).Where(x => x.TestId == _executionId)).IsEmpty();
    }

    [Test]
    public async Task Factory_failure_keeps_inner_exception_details_in_the_html_report()
    {
        await Assert.That(async () => await ScenarioOutline("Nested factory error",
            new[] { new { Value = 1 } }, row =>
            {
                throw new TypeInitializationException("ExampleFactory", new InvalidOperationException("<inner factory failure>"));
            })).ThrowsExactly<TypeInitializationException>();
        var outline = BDTestUtil.GetScenarios(TestRun).Single(x => x.FrameworkTestId == _executionId);
        var error = Required(outline.Examples).Single().Exception;
        var html = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);

        await Assert.That(html).Contains(Required(System.Net.WebUtility.HtmlEncode(Required(error).AsString)));

        await Assert.That(html).Contains("&lt;inner factory failure&gt;").And.DoesNotContain("<inner factory failure>");
    }

    [Test]
    public async Task Rejected_foreign_builder_is_removed_without_discarding_unrelated_pending_scenarios()
    {
        var foreign = new SkipCapture(TestRun);
        var unrelated = foreign.Given(() => NoOp()).When(() => NoOp()).Then(() => NoOp());
        RunnableStepBuilder? rejected = null;
        try
        {
            var exception = await Assert.That(async () => await ScenarioOutline("Foreign builder",
                new[] { new { Value = 1 }, new { Value = 2 } }, row => row.Value == 1
                    ? rejected = foreign.Given(() => NoOp()).When(() => NoOp()).Then(() => NoOp())
                    : Given(() => Record(row.Value)).When(() => NoOp()).Then(() => NoOp()))).ThrowsExactly<InvalidOperationException>();

            await Assert.That(Required(exception).Message).IsEqualTo("Example scenarios must be built by the outline's test instance.");
            var outline = BDTestUtil.GetScenarios(TestRun).Single(x => x.FrameworkTestId == _executionId);
            await Assert.That(Required(outline.Examples).Select(row => row.Status)).IsEquivalentTo(new[] { Status.Failed, Status.Passed }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(_visited).IsEquivalentTo(new[] { 2 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
            var pending = BDTestUtil.GetNotRunScenarios(TestRun).Select(item => item.ScenarioId).ToArray();
            await Assert.That(pending).DoesNotContain(rejected?.Guid);
            await Assert.That(pending).Contains(unrelated.Guid);
        }
        finally
        {
            TestRun.Store.NotRun.TryRemove(Required(unrelated.Guid), out _);
            if (rejected != null) TestRun.Store.NotRun.TryRemove(Required(rejected.Guid), out _);
        }
    }

    [Test]
    [Arguments("success", Status.Passed, false)]
    [Arguments("skipped", Status.Skipped, false)]
    [Arguments("inconclusive", Status.Inconclusive, false)]
    [Arguments("not-implemented", Status.NotImplemented, false)]
    [Arguments("success", Status.Passed, true)]
    [Arguments("skipped", Status.Skipped, true)]
    [Arguments("inconclusive", Status.Inconclusive, true)]
    [Arguments("not-implemented", Status.NotImplemented, true)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "TUnit0018", Justification = "TUnit creates a fresh fixture for each case; these fields configure that case's outline context and retry hooks.")]
    public async Task Factory_and_context_outcomes_preserve_framework_classification(string outcome, Status rowStatus, bool duringContext)
    {
        var contextCount = 0;
        if (duringContext)
            Configure = _ => { if (++contextCount == 1) Outcome(outcome); };
        Exception? thrown = null;
        try
        {
            await ScenarioOutline("Factory outcomes", new[] { new { Value = 1 }, new { Value = 2 } }, row =>
            {
                if (!duringContext && row.Value == 1) Outcome(outcome);
                return Given(() => Record(row.Value)).When(() => NoOp()).Then(() => NoOp());
            });
        }
        catch (Exception exception) { thrown = exception; }

        var outline = BDTestUtil.GetScenarios(TestRun).Single(x => x.FrameworkTestId == _executionId);
        await Assert.That(Required(outline.Examples).Select(row => row.Status)).IsEquivalentTo(new[] { rowStatus, Status.Passed }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(_visited).IsEquivalentTo(new[] { 2 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var expected = rowStatus == Status.Skipped ? Status.Passed : rowStatus;
        await Assert.That(outline.Status).IsEqualTo(expected);
        if (expected == Status.Passed) await Assert.That(thrown).IsNull();
        else await Assert.That(thrown).IsNotNull();
        var row = outline.Examples[0];
        if (rowStatus is Status.Passed or Status.Skipped or Status.Inconclusive)
        {
            await Assert.That(row.Exception).IsNull();
            await Assert.That(outline.Exception).IsNull();
            var restored = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(outline));
            await Assert.That(Required(restored.Examples)[0].Exception).IsNull();
            await Assert.That(restored.Exception).IsNull();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "TUnit0018", Justification = "TUnit creates a fresh fixture for each case; these fields configure that case's outline context and retry hooks.")]
    public async Task Construction_does_not_expose_an_outline_or_previous_example(bool fail)
    {
        var amendmentScenarios = new List<Scenario?>();
        var factoryScenarios = new List<Scenario?>();
        var stepScenarios = new List<Scenario?>();
        Configure = context => amendmentScenarios.Add(context.CurrentScenario);
        Exception? thrown = null;
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


        if (fail) await Assert.That(thrown).IsTypeOf<InvalidOperationException>();
        else await Assert.That(thrown).IsNull();
        await Assert.That(amendmentScenarios).Count().IsEqualTo(3);

        await Assert.That((amendmentScenarios).All(item => item is null)).IsTrue();
        await Assert.That(factoryScenarios).Count().IsEqualTo(3);

        await Assert.That((factoryScenarios).All(item => item is null)).IsTrue();
        var outline = BDTestUtil.GetScenarios(TestRun).Single(x => x.FrameworkTestId == _executionId);
        await Assert.That(stepScenarios).IsEquivalentTo(Required(outline.Examples).Where(row => row.Status == Status.Passed).Cast<Scenario?>(), EqualityComparer<Scenario?>.Default, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(BDTestContext.CurrentScenario).IsSameReferenceAs(outline);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "TUnit0018", Justification = "TUnit creates a fresh fixture for each case; these fields configure that case's outline context and retry hooks.")]
    public async Task Construction_output_belongs_to_its_example_even_when_construction_fails(bool duringContext, bool fail)
    {
        var contextCount = 0;
        Configure = _ =>
        {
            var value = ++contextCount;
            Console.WriteLine($"context output {value} {_executionId}");
            ScenarioHtmlWriter.Text($"context markup {value}");
            if (duringContext && fail && value == 2) throw new InvalidOperationException("context failed");
        };
        Exception? thrown = null;
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


        if (fail) await Assert.That(thrown).IsTypeOf<InvalidOperationException>();
        else await Assert.That(thrown).IsNull();
        var outline = BDTestUtil.GetScenarios(TestRun).Single(x => x.FrameworkTestId == _executionId);
        var restored = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(outline));
        foreach (var result in new[] { outline, restored })
        {
            await Assert.That(result.Examples).Count().IsEqualTo(3);
            for (var index = 0; index < Required(result.Examples).Count; index++)
            {
                var row = result.Examples[index];
                var value = index + 1;
                await Assert.That(row.Output).Contains($"context output {value}");
                await Assert.That(row.CustomHtmlOutputForReport).StartsWith($"<span>context markup {value}</span><br>");
                if (!(duringContext && fail && value == 2))
                {
                    await Assert.That(row.Output).Contains($"factory output {value}");
                    await Assert.That(row.Output).Contains($"built output {value}");
                    await Assert.That(row.CustomHtmlOutputForReport).Contains($"<span>factory markup {value}</span><br><span>built markup {value}</span><br>");
                }
                foreach (var other in Enumerable.Range(1, 3).Where(other => other != value))
                {
                    await Assert.That(row.Output).DoesNotContain($"output {other}");
                    await Assert.That(row.CustomHtmlOutputForReport).DoesNotContain($"markup {other}");
                }
                if (!fail || value != 2)
                    await Assert.That(row.CustomHtmlOutputForReport).EndsWith($"<span>step markup {value}</span><br>");

                await Assert.That(row.TestStartupInformation).IsNullOrEmpty();

                await Assert.That(row.TearDownOutput).IsNullOrEmpty();
            }

            await Assert.That(result.CustomHtmlOutputForReport).IsNullOrEmpty();

            await Assert.That(result.Output).Contains("context output 1").And.Contains("context output 2").And.Contains("context output 3");
        }
        var razor = await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioExamples.cshtml", restored);
        var fullStandalone = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);
        foreach (var value in Enumerable.Range(1, 3))
            await Assert.That(System.Text.RegularExpressions.Regex.Matches(fullStandalone, $"context output {value} {_executionId}").Count).IsEqualTo(1);
        var standalone = System.Text.RegularExpressions.Regex.Match(fullStandalone,
            $"<table class=\"examples\" id=\"examples-{outline.Guid}\">.*?</table>",
            System.Text.RegularExpressions.RegexOptions.Singleline).Value;
        foreach (var html in new[] { razor, standalone })

            await Assert.That(html).Contains("context output 1").And.Contains("context output 2").And.Contains("context output 3");
    }

    private RunnableStepBuilder BuildRow(int value)
    {
        var builder = Given(() => Record(value)).When(() => NoOp()).Then(() => NoOp());
        if (value == 1) throw new InvalidOperationException("factory failed");
        return builder;
    }

    [Test]
    [Arguments(false, Status.Passed, 0)]
    [Arguments(true, Status.Skipped, 1)]
    public async Task Rule_skips_substitute_row_text_and_notify_the_adapter_once(bool skipBoth, Status status, int notifications)
    {
        var capture = new SkipCapture(TestRun);
        var outline = await capture.ScenarioOutline("Rule skips", new[] { new { Skip = true }, new { Skip = skipBoth } },
            row => capture.Given(() => NoOp()).WithStepText(() => "skip <Skip>").When(() => NoOp()).Then(() => NoOp()));
        await Assert.That(outline.Status).IsEqualTo(status);
        await Assert.That(Required(Required(outline.Examples)[0].Steps)[0].StepText).IsEqualTo("Given skip True");
        await Assert.That(capture.Notifications).IsEqualTo(notifications);
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
            await Assert.That(async () => await builder.BDTestAsync()).ThrowsExactly<AlreadyExecutedException>();
            await Assert.That(async () => await builder).ThrowsExactly<AlreadyExecutedException>();
        }
        finally { finish.TrySetResult(); }
        await Assert.That((await execution).Examples).Count().IsEqualTo(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task All_entry_points_share_a_single_execution_guard(bool directAwait)
    {
        var builder = ScenarioOutline("Once", new[] { new { Value = 1 } },
            row => Given(() => Record(row.Value)).When(() => NoOp()).Then(() => NoOp()));
        var outline = directAwait ? await builder : await builder.BDTestAsync();
        await Assert.That(async () => await builder.BDTestAsync()).ThrowsExactly<AlreadyExecutedException>();
        await Assert.That(async () => await builder).ThrowsExactly<AlreadyExecutedException>();
        await Assert.That(outline.Examples).Count().IsEqualTo(1);
        WriteTearDownOutput("outline teardown");
        await Assert.That(outline.TearDownOutput).Contains("outline teardown");

        await Assert.That(Required(outline.Examples)[0].TearDownOutput).IsNullOrEmpty();
    }

    [Test]
    public async Task Text_substitution_is_invariant_and_does_not_expand_values_recursively()
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            var outline = await ScenarioOutline("Text", new[] { new { Number = 1.5m, Text = "<Number>", Empty = (string?)null } },
                row => Given(() => Template()).When(() => NoOp()).Then(() => NoOp()));
            await Assert.That(Required(Required(outline.Examples)[0].Steps)[0].StepText).IsEqualTo("Given 1.5 <Number> null <Unknown>");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
    }

    [Test]
    public async Task Html_and_Razor_reports_show_encoded_examples_under_one_outline()
    {
        var outline = await ScenarioOutline("Report outline", new[] { new { Text = "<script>row</script>" }, new { Text = "second" } },
            row => Given(() => NoOp()).WithStepText(() => "value <Text>").When(() => NoOp()).Then(() => NoOp()));
        var restored = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(outline));
        var standalone = BDTestHtmlHelper.GetTestHtmlReport(null, TestRun);
        var exampleTable = System.Text.RegularExpressions.Regex.Match(standalone,
            $"<table class=\"examples\" id=\"examples-{outline.Guid}\">.*?</table>", System.Text.RegularExpressions.RegexOptions.Singleline).Value;
        await Assert.That(exampleTable).IsNotEmpty();
        foreach (var html in new[]
        {
            "Examples" + exampleTable,
            await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioExamples.cshtml", restored),
            await ReportHandlerEncodingTests.RenderAsync("/Views/Shared/_ScenarioPanel.cshtml", new ScenarioModalViewModel { Scenario = restored }),
            await ReportHandlerEncodingTests.RenderAsync("/Views/BDTest/Scenario.cshtml", restored)
        })
        {
            await Assert.That(html).Contains("Examples");
            await Assert.That(html).Contains("&lt;script&gt;row&lt;/script&gt;");
            await Assert.That(html).DoesNotContain("<script>row</script>");
            await Assert.That(System.Text.RegularExpressions.Regex.Matches(html, "class=\"example-row\"").Count).IsEqualTo(2);
            // The standalone report shows the step keyword in its own column.
            var text = System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", " "), @"\s+", " ");
            await Assert.That(text).Contains("Given value");
        }
    }

    private void Record(int value)
    {
        RequireCondition(Context.Value == 0, "Each outline row must start with a fresh context.");
        Context.Value = value;
        RequireCondition(Required(BDTestContext.CurrentScenario).OutlineId != null, "Each row must retain its outline identity.");
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
    private class SkipCapture(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
        private readonly string _id = Guid.NewGuid().ToString();
        protected override string BDTestExecutionId => _id;
        public int Notifications { get; private set; }
        protected override void OnScenarioSkipped(string reason) => Notifications++;
    }
    public class RowContext { public int Value { get; set; } }
}
