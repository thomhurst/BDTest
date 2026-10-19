using System.Collections;
using System.Reflection;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Interfaces;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Tests.Helpers;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class StepTextGenerationTests : RunContextTestBase<StepTextGenerationTests.TextContext>
{
    protected override bool BindCurrentTestMetadata => true;

    [After(HookType.Test)]
    public void FinishExecution() => EndTest();

    private static readonly MethodInfo DisplayMethod = Required(typeof(StepTextGenerationTests).GetMethod(nameof(Display)));
    private readonly string _executionId = Guid.NewGuid().ToString();
    protected override string BDTestExecutionId => _executionId;

    public static IEnumerable<(object? value, string expected)> CollectionCases()
    {
        yield return (new List<int> { 1, 2 }, "1, 2");
        yield return (new[] { 3, 4 }, "3, 4");
        yield return (new ArrayList { 5, "six" }, "5, six");
        yield return (new Dictionary<string, int> { ["one"] = 1, ["two"] = 2 }, "one=1,two=2");
        yield return (new Hashtable { ["three"] = 3 }, "three=3");
        yield return (new List<int>(), "");
        yield return (new Dictionary<string, int>(), "");
        yield return ("text", "text");
        yield return (null, "null");
    }

    [Test]
    [MethodDataSource(nameof(CollectionCases))]
    public async Task FormatsValuesWithoutLosingCollectionElements(object? value, string expected)
    {
        await Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => value], run: TestRun)).IsEqualTo($"value: {expected}");
    }

    [Test]
    public async Task ExplicitArgumentReadersRetainTheirTypeForConverters()
    {
        var converter = new NumberConverter();
        Options.RegisterCustomStringConverter(converter);
        var method = Required(typeof(StepTextGenerationTests).GetMethod(nameof(DisplayNumber)));
        await Assert.That(StepTextHelper.GetStepText(method, [() => 42], run: TestRun)).IsEqualTo("number: converted 42");

    }

    [Test]
    public async Task FindsExplicitConvertersAmongMultipleInterfacesAndAcceptsDerivedValues()
    {
        var converter = new MultipleConverter();
        Options.RegisterCustomStringConverter<BaseValue>(converter);
        Options.RegisterCustomStringConverter<List<int>>(converter);
        var value = new DerivedValue();
        var numbers = new List<int> { 1, 2 };
        await Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => value], run: TestRun)).IsEqualTo("value: base value");
        await Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => numbers], run: TestRun)).IsEqualTo("value: converted collection");

    }

    [Test]
    public async Task FormattingDoesNotInvokeDelegateArguments()
    {
        var calls = 0;
        Func<string> func = () => { calls++; return "result"; };
        Action action = () => calls++;
        CustomDelegate custom = () => { calls++; return "custom"; };

        await Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => func], run: TestRun)).IsEqualTo($"value: {func}");
        await Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => action], run: TestRun)).IsEqualTo($"value: {action}");
        await Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => custom], run: TestRun)).IsEqualTo($"value: {custom}");
        await Assert.That(calls).IsZero();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task GeneratedTextIsRenderedOncePerExecutionAndBuilderCopiesAreIndependent(bool asyncStep)
    {
        var source = new ArgumentSource();
        var prefix = asyncStep
            ? When(() => DisplayAsync(source.GetValue())).WithArgs(() => source.GetValue())
            : When(() => Display(source.GetValue())).WithArgs(() => source.GetValue());
        var first = prefix.Then(() => Display("first"));
        var second = prefix.Then(() => Display("second"));
        await Assert.That(source.Calls).IsZero();

        var firstScenario = await first.BDTestAsync();
        var secondScenario = await second.BDTestAsync();

        await Assert.That(source.Calls).IsEqualTo(4).Because("One text evaluation and one invocation for each execution");
        await Assert.That(Required(firstScenario.Steps)[0].StepText).IsEqualTo("When value: 1");
        await Assert.That(Required(secondScenario.Steps)[0].StepText).IsEqualTo("When value: 3");
    }

    [Test]
    public async Task ReportingAnUnexecutedStepDoesNotEvaluateItsArguments()
    {
        var source = new ArgumentSource();
        var builder = When(() => Fail()).Then(() => Display(source.GetValue())).WithArgs(() => source.GetValue());

        await Assert.That(async () => await builder.BDTestAsync()).ThrowsExactly<InvalidOperationException>();

        await Assert.That(source.Calls).IsZero();
        await Assert.That(Required(BDTestUtil.GetScenarios(TestRun).Single().Steps)[1].StepText).IsEqualTo("Then value: {value}");
    }

    [Test]
    [BDTestRetry(1)]
    public async Task RetryingRendersArgumentsAgain()
    {
        var source = new ArgumentSource();
        var scenario = await When(() => source.FailOnce(source.GetValue())).WithArgs(() => source.GetValue())
            .Then(() => Display("finished"))
            .BDTestAsync();

        await Assert.That(scenario.RetryCount).IsEqualTo(1);
        await Assert.That(source.Calls).IsEqualTo(4).Because("One text evaluation and one invocation per attempt");
        await Assert.That(Required(scenario.Steps)[0].StepText).IsEqualTo("When value: 3");
    }

    [Test]
    [Arguments("dynamic", "Then dynamic")]
    [Arguments("", "Then value: 1")]
    public async Task OverridesRemainDynamicAndEmptyOverridesRenderArguments(string updatedText, string expected)
    {
        var source = new ArgumentSource();
        var text = "initial";
        Action update = () => text = updatedText;
        var scenario = await When(() => SetText(update))
            .Then(() => Display(source.GetValue())).WithArgs(() => source.GetValue()).WithStepText(() => text)
            .BDTestAsync();

        await Assert.That(source.Calls).IsEqualTo(updatedText == "" ? 2 : 1);
        await Assert.That(Required(scenario.Steps)[1].StepText).IsEqualTo(expected);
    }

    [Test]
    public async Task ArgumentTextReadsContextAfterEarlierStepsAndBeforeTheStepBody()
    {
        var builder = Given(() => SetContextValue(42))
            .When(() => Display(Context.Value))
            .Then(() => Display("finished"));
        await Assert.That(Context.Reads).IsZero();

        var scenario = await builder.BDTestAsync();

        await Assert.That(Required(scenario.Steps)[1].StepText).IsEqualTo("When value: 42");
        await Assert.That(Context.Reads).IsEqualTo(2).Because("One read for text and one for the step body");
    }

    [Test, BDTestRetry(1)]
    public async Task RetryRendersAgainstRecreatedContext()
    {
        var contexts = new List<TextContext>();
        var source = new ArgumentSource();
        var scenario = await Given(() => InitializeContext(contexts))
            .When(() => source.FailOnce(Context.Value)).WithArgs(() => Context.Value)
            .Then(() => Display("finished"))
            .BDTestAsync();

        await Assert.That(contexts).Count().IsEqualTo(2);
        await Assert.That(contexts[0]).IsNotSameReferenceAs(contexts[1]);
        await Assert.That(contexts.Select(context => context.Reads)).IsEquivalentTo(new[] { 2, 2 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(Required(scenario.Steps)[1].StepText).IsEqualTo("When value: 2");
    }

    [Test]
    [Arguments(false), Arguments(true)]
    public async Task SkippedStepsDoNotReadAutomaticArguments(bool skipScenario)
    {
        var source = new ArgumentSource();
        var skip = skipScenario;
        Options.SkipTestRules.Add(scenario => skip && scenario.FrameworkTestId == _executionId);
        Options.Debug.ShouldSkipWhenStep = !skipScenario;
        try
        {
            var scenario = await When(() => Display(source.GetValue())).WithArgs(() => source.GetValue()).Then(() => Display("finished")).BDTestAsync();
            await Assert.That(source.Calls).IsZero();
            await Assert.That(Required(scenario.Steps)[0].Status).IsEqualTo(Status.Skipped);
            await Assert.That(scenario.Steps[0].StepText).EndsWith("When value: {value}");
        }
        finally
        {
            skip = false;
        }
    }

    [Test]
    public async Task ArgumentEvaluationErrorsAreExplicitAndDoNotFailTheStep()
    {
        var source = new TransientArgumentSource();
        var scenario = await When(() => Display(source.Value)).WithArgs(() => source.Value).Then(() => Display("finished")).BDTestAsync();

        var step = Required(scenario.Steps)[0];
        await Assert.That(step.Status).IsEqualTo(Status.Passed);
        await Assert.That(step.Exception).IsNull();
        await Assert.That(step.StepText).IsEqualTo("When value: <error: NullReferenceException>");

        await Assert.That(Required(step.Diagnostics).Single()).Contains("value").And.Contains("NullReferenceException").And.Contains("not ready");
        var restored = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(scenario));
        await Assert.That(Required(restored.Steps)[0].Diagnostics).IsEquivalentTo(step.Diagnostics, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task ConverterErrorsAreExplicitAndDoNotFailTheStep()
    {
        var converter = new ThrowingConverter();
        Options.RegisterCustomStringConverter(converter);
        var scenario = await When(() => Display(new UnformattableValue())).WithArgs(() => new UnformattableValue()).Then(() => Display("finished")).BDTestAsync();
        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
        await Assert.That(Required(scenario.Steps)[0].StepText).IsEqualTo("When value: <error: InvalidOperationException>");
        await Assert.That(Required(scenario.Steps[0].Diagnostics).Single()).Contains("conversion failed");

    }

    [Test]
    public async Task AttributeSkippedStepsDoNotEvaluateArgumentsEvenWithAnOverride()
    {
        Options.SkipStepRules.Add<SkipTextStepAttribute>(_ => true);
        var source = new ArgumentSource();
        var scenario = await When(() => SkippedDisplay(source.GetValue())).WithArgs(() => source.GetValue()).WithStepText(() => "custom skip")
            .Then(() => Display("finished")).BDTestAsync();

        await Assert.That(source.Calls).IsZero();
        await Assert.That(Required(scenario.Steps)[0].Status).IsEqualTo(Status.Skipped);
        await Assert.That(scenario.Steps[0].StepText).IsEqualTo("[Skipped] When custom skip");
    }

    [Test]
    public async Task TextReflectsChangesMadeByBeforeStepHooks()
    {
        var value = 0;
        Func<BDTest.Contracts.StepResult, Task> hook = step => { value++; return Task.CompletedTask; };
        Options.StepHooks.Add(new CallbackStepHook(before: hook));
        var scenario = await When(() => Display(value)).WithArgs(() => value).Then(() => Display(value)).WithArgs(() => value).BDTestAsync();
        await Assert.That(Required(scenario.Steps).Select(step => step.StepText)).IsEquivalentTo((new[] { "When value: 1", "Then value: 2" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);

    }

    [Test, BDTestRetry(1)]
    public async Task RetryClearsEarlierDiagnosticsAndRenderedArgumentsOnUnreachedSteps()
    {
        var source = new TransientArgumentSource();
        var attempt = 0;
        Func<int> nextAttempt = () => ++attempt;
        var builder = Given(() => FailOnSecondAttempt(nextAttempt))
            .When(() => Display(source.Value)).WithArgs(() => source.Value)
            .Then(() => Fail());

        await Assert.That(async () => await builder.BDTestAsync()).ThrowsExactly<InvalidOperationException>();
        var step = Required(BDTestUtil.GetScenarios(TestRun).Single().Steps)[1];
        await Assert.That(step.Status).IsEqualTo(Status.NotRun);
        await Assert.That(step.StepText).IsEqualTo("When value: {value}");
        await Assert.That(step.Diagnostics).IsEmpty();
    }

    [SkipTextStep, StepText("value: {0}")]
    private static void SkippedDisplay(object value) => throw new Exception("Skipped step must not run");
    private sealed class SkipTextStepAttribute : SkipStepAttribute { }
    private static void FailOnSecondAttempt(Func<int> nextAttempt)
    {
        if (nextAttempt() == 2) Fail();
    }

    private void SetContextValue(int value) => Context.SetValue(value);
    private void InitializeContext(List<TextContext> contexts)
    {
        contexts.Add(Context);
        Context.SetValue(contexts.Count);
    }

    public class TextContext
    {
        private int _value;
        public int Reads { get; private set; }
        public int Value { get { Reads++; return _value; } }
        public void SetValue(int value) => _value = value;
    }

    private sealed class TransientArgumentSource
    {
        private int _calls;
        public string Value => _calls++ == 0 ? throw new NullReferenceException("not ready") : "ready";
    }

    private sealed class UnformattableValue { }
    private sealed class ThrowingConverter : IStepTextStringConverter<UnformattableValue>
    {
        public string ConvertToString(UnformattableValue value) => throw new InvalidOperationException("conversion failed");
    }

    [StepText("value: {0}")]
    public static void Display(object value) { }

    [StepText("number: {0}")]
    public static void DisplayNumber(int value) { }

    [StepText("value: {0}")]
    public static Task DisplayAsync(object value) => Task.CompletedTask;

    private static void Fail() => throw new InvalidOperationException("Expected failure");
    private static void SetText(Action action) => action();
    private delegate string CustomDelegate();
    private class BaseValue { }
    private class DerivedValue : BaseValue { }

    private sealed class ArgumentSource
    {
        public int Calls { get; private set; }
        private int _attempts;
        public int GetValue() => ++Calls;

        [StepText("value: {0}")]
        public void FailOnce(int value)
        {
            if (_attempts++ == 0)
            {
                Fail();
            }
        }
    }

    private sealed class NumberConverter : IStepTextStringConverter<int>
    {
        public string ConvertToString(int value) => $"converted {value}";
    }

    private sealed class MultipleConverter : IDisposable, IStepTextStringConverter<BaseValue>, IStepTextStringConverter<IEnumerable<int>>
    {
        public void Dispose() { }
        string IStepTextStringConverter<BaseValue>.ConvertToString(BaseValue value) => "base value";
        string IStepTextStringConverter<IEnumerable<int>>.ConvertToString(IEnumerable<int> value) => "converted collection";
    }
}
