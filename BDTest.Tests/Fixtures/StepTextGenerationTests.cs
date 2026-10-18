using System.Collections;
using System.Linq.Expressions;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Interfaces;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class StepTextGenerationTests : AbstractContextBDTestBase<StepTextGenerationTests.TextContext>
{
    private readonly string _executionId = Guid.NewGuid().ToString();
    protected override string BDTestExecutionId => _executionId;

    [SetUp]
    public void Setup() => TestResetHelper.ResetData();

    private static IEnumerable<TestCaseData> CollectionCases()
    {
        yield return new TestCaseData(new List<int> { 1, 2 }, "1, 2");
        yield return new TestCaseData(new[] { 3, 4 }, "3, 4");
        yield return new TestCaseData(new ArrayList { 5, "six" }, "5, six");
        yield return new TestCaseData(new Dictionary<string, int> { ["one"] = 1, ["two"] = 2 }, "one=1,two=2");
        yield return new TestCaseData(new Hashtable { ["three"] = 3 }, "three=3");
        yield return new TestCaseData(new List<int>(), "");
        yield return new TestCaseData(new Dictionary<string, int>(), "");
        yield return new TestCaseData("text", "text");
        yield return new TestCaseData(null, "null");
    }

    [TestCaseSource(nameof(CollectionCases))]
    public void FormatsValuesWithoutLosingCollectionElements(object value, string expected)
    {
        Assert.That(StepTextHelper.GetStepText(() => Display(value)), Is.EqualTo($"value: {expected}"));
    }

    [Test]
    public void ConstantExpressionsRetainTheirTypeForConverters()
    {
        var converter = new NumberConverter();
        BDTestSettings.CustomStringConverters.Add(converter);
        try
        {
            var call = Expression.Call(typeof(StepTextGenerationTests).GetMethod(nameof(DisplayNumber)), Expression.Constant(42));
            Assert.That(StepTextHelper.GetStepText(call), Is.EqualTo("number: converted 42"));
        }
        finally
        {
            BDTestSettings.CustomStringConverters.Remove(converter);
        }
    }

    [Test]
    public void FindsExplicitConvertersAmongMultipleInterfacesAndAcceptsDerivedValues()
    {
        var converter = new MultipleConverter();
        BDTestSettings.CustomStringConverters.Add(converter);
        try
        {
            var value = new DerivedValue();
            var numbers = new List<int> { 1, 2 };
            Assert.That(StepTextHelper.GetStepText(() => Display(value)), Is.EqualTo("value: base value"));
            Assert.That(StepTextHelper.GetStepText(() => Display(numbers)), Is.EqualTo("value: converted collection"));
        }
        finally
        {
            BDTestSettings.CustomStringConverters.Remove(converter);
        }
    }

    [Test]
    public void FormattingDoesNotInvokeDelegateArguments()
    {
        var calls = 0;
        Func<string> func = () => { calls++; return "result"; };
        Action action = () => calls++;
        CustomDelegate custom = () => { calls++; return "custom"; };

        Assert.That(StepTextHelper.GetStepText(() => Display(func)), Is.EqualTo($"value: {func}"));
        Assert.That(StepTextHelper.GetStepText(() => Display(action)), Is.EqualTo($"value: {action}"));
        Assert.That(StepTextHelper.GetStepText(() => Display(custom)), Is.EqualTo($"value: {custom}"));
        Assert.That(calls, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task GeneratedTextIsRenderedOncePerExecutionAndBuilderCopiesAreIndependent(bool asyncStep)
    {
        var source = new ArgumentSource();
        var prefix = asyncStep
            ? When(() => DisplayAsync(source.GetValue()))
            : When(() => Display(source.GetValue()));
        var first = prefix.Then(() => Display("first"));
        var second = prefix.Then(() => Display("second"));
        Assert.That(source.Calls, Is.Zero);

        var firstScenario = await first.BDTestAsync();
        var secondScenario = await second.BDTestAsync();

        Assert.That(source.Calls, Is.EqualTo(4), "One text evaluation and one invocation for each execution");
        Assert.That(firstScenario.Steps[0].StepText, Is.EqualTo("When value: 1"));
        Assert.That(secondScenario.Steps[0].StepText, Is.EqualTo("When value: 3"));
    }

    [Test]
    public void ReportingAnUnexecutedStepDoesNotEvaluateItsArguments()
    {
        var source = new ArgumentSource();
        var builder = When(() => Fail()).Then(() => Display(source.GetValue()));

        Assert.ThrowsAsync<InvalidOperationException>(async () => await builder.BDTestAsync());

        Assert.That(source.Calls, Is.Zero);
        Assert.That(BDTestUtil.GetScenarios().Single().Steps[1].StepText, Is.EqualTo("Then value: {value}"));
    }

    [Test]
    [BDTestRetry(1)]
    public async Task RetryingRendersArgumentsAgain()
    {
        var source = new ArgumentSource();
        var scenario = await When(() => source.FailOnce(source.GetValue()))
            .Then(() => Display("finished"))
            .BDTestAsync();

        Assert.That(scenario.RetryCount, Is.EqualTo(1));
        Assert.That(source.Calls, Is.EqualTo(4), "One text evaluation and one invocation per attempt");
        Assert.That(scenario.Steps[0].StepText, Is.EqualTo("When value: 3"));
    }

    [TestCase("dynamic", "Then dynamic")]
    [TestCase("", "Then value: 1")]
    public async Task OverridesRemainDynamicAndEmptyOverridesRenderArguments(string updatedText, string expected)
    {
        var source = new ArgumentSource();
        var text = "initial";
        Action update = () => text = updatedText;
        var scenario = await When(() => SetText(update))
            .Then(() => Display(source.GetValue())).WithStepText(() => text)
            .BDTestAsync();

        Assert.That(source.Calls, Is.EqualTo(updatedText == "" ? 2 : 1));
        Assert.That(scenario.Steps[1].StepText, Is.EqualTo(expected));
    }

    [Test]
    public async Task ArgumentTextReadsContextAfterEarlierStepsAndBeforeTheStepBody()
    {
        var builder = Given(() => SetContextValue(42))
            .When(() => Display(Context.Value))
            .Then(() => Display("finished"));
        Assert.That(Context.Reads, Is.Zero);

        var scenario = await builder.BDTestAsync();

        Assert.That(scenario.Steps[1].StepText, Is.EqualTo("When value: 42"));
        Assert.That(Context.Reads, Is.EqualTo(2), "One read for text and one for the step body");
    }

    [Test, BDTestRetry(1)]
    public async Task RetryRendersAgainstRecreatedContext()
    {
        var contexts = new List<TextContext>();
        var source = new ArgumentSource();
        var scenario = await Given(() => InitializeContext(contexts))
            .When(() => source.FailOnce(Context.Value))
            .Then(() => Display("finished"))
            .BDTestAsync();

        Assert.That(contexts, Has.Count.EqualTo(2));
        Assert.That(contexts[0], Is.Not.SameAs(contexts[1]));
        Assert.That(contexts.Select(context => context.Reads), Is.EqualTo(new[] { 2, 2 }));
        Assert.That(scenario.Steps[1].StepText, Is.EqualTo("When value: 2"));
    }

    [TestCase(false), TestCase(true)]
    public async Task SkippedStepsDoNotReadAutomaticArguments(bool skipScenario)
    {
        var source = new ArgumentSource();
        var skip = skipScenario;
        BDTestSettings.GlobalSkipTestRules.Add(scenario => skip && scenario.FrameworkTestId == _executionId);
        var previousSkip = BDTestSettings.Debug.ShouldSkipWhenStep;
        BDTestSettings.Debug.ShouldSkipWhenStep = !skipScenario;
        try
        {
            var scenario = await When(() => Display(source.GetValue())).Then(() => Display("finished")).BDTestAsync();
            Assert.That(source.Calls, Is.Zero);
            Assert.That(scenario.Steps[0].Status, Is.EqualTo(Status.Skipped));
            Assert.That(scenario.Steps[0].StepText, Does.EndWith("When value: {value}"));
        }
        finally
        {
            skip = false;
            BDTestSettings.Debug.ShouldSkipWhenStep = previousSkip;
        }
    }

    [Test]
    public async Task ArgumentEvaluationErrorsAreExplicitAndDoNotFailTheStep()
    {
        var source = new TransientArgumentSource();
        var scenario = await When(() => Display(source.Value)).Then(() => Display("finished")).BDTestAsync();

        var step = scenario.Steps[0];
        Assert.That(step.Status, Is.EqualTo(Status.Passed));
        Assert.That(step.Exception, Is.Null);
        Assert.That(step.StepText, Is.EqualTo("When value: <error: NullReferenceException>"));
        Assert.That(step.Diagnostics.Single(), Does.Contain("value").And.Contain("NullReferenceException").And.Contain("not ready"));
        var restored = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(scenario));
        Assert.That(restored.Steps[0].Diagnostics, Is.EqualTo(step.Diagnostics));
    }

    [Test]
    public async Task ConverterErrorsAreExplicitAndDoNotFailTheStep()
    {
        var converter = new ThrowingConverter();
        BDTestSettings.CustomStringConverters.Add(converter);
        try
        {
            var scenario = await When(() => Display(new UnformattableValue())).Then(() => Display("finished")).BDTestAsync();
            Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
            Assert.That(scenario.Steps[0].StepText, Is.EqualTo("When value: <error: InvalidOperationException>"));
            Assert.That(scenario.Steps[0].Diagnostics.Single(), Does.Contain("conversion failed"));
        }
        finally
        {
            BDTestSettings.CustomStringConverters.Remove(converter);
        }
    }

    [Test]
    public async Task AttributeSkippedStepsDoNotEvaluateArgumentsEvenWithAnOverride()
    {
        BDTestSettings.GlobalSkipStepRules.Add<SkipTextStepAttribute>(_ => true);
        var source = new ArgumentSource();
        var scenario = await When(() => SkippedDisplay(source.GetValue())).WithStepText(() => "custom skip")
            .Then(() => Display("finished")).BDTestAsync();

        Assert.That(source.Calls, Is.Zero);
        Assert.That(scenario.Steps[0].Status, Is.EqualTo(Status.Skipped));
        Assert.That(scenario.Steps[0].StepText, Is.EqualTo("[Skipped] When custom skip"));
    }

    [Test]
    public async Task TextReflectsChangesMadeByBeforeStepHooks()
    {
        var value = 0;
        Func<Step, Task> hook = step => { value++; return Task.CompletedTask; };
        BDTestSettings.BeforeStep += hook;
        try
        {
            var scenario = await When(() => Display(value)).Then(() => Display(value)).BDTestAsync();
            Assert.That(scenario.Steps.Select(step => step.StepText), Is.EqualTo(new[] { "When value: 1", "Then value: 2" }));
        }
        finally
        {
            BDTestSettings.BeforeStep -= hook;
        }
    }

    [Test, BDTestRetry(1)]
    public void RetryClearsEarlierDiagnosticsAndRenderedArgumentsOnUnreachedSteps()
    {
        var source = new TransientArgumentSource();
        var attempt = 0;
        Func<int> nextAttempt = () => ++attempt;
        var builder = Given(() => FailOnSecondAttempt(nextAttempt))
            .When(() => Display(source.Value))
            .Then(() => Fail());

        Assert.ThrowsAsync<InvalidOperationException>(async () => await builder.BDTestAsync());
        var step = BDTestUtil.GetScenarios().Single().Steps[1];
        Assert.That(step.Status, Is.EqualTo(Status.NotRun));
        Assert.That(step.StepText, Is.EqualTo("When value: {value}"));
        Assert.That(step.Diagnostics, Is.Empty);
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
