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
public class StepTextGenerationTests : BDTestBase
{
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
    public async Task GeneratedTextIsCachedAcrossExecutionAndBuilderCopies(bool asyncStep)
    {
        var source = new ArgumentSource();
        var prefix = asyncStep
            ? When(() => DisplayAsync(source.GetValue()))
            : When(() => Display(source.GetValue()));
        var first = prefix.Then(() => Display("first"));
        var second = prefix.Then(() => Display("second"));
        Assert.That(source.Calls, Is.EqualTo(1));

        var firstScenario = await first.BDTestAsync();
        var secondScenario = await second.BDTestAsync();

        Assert.That(source.Calls, Is.EqualTo(3), "One text evaluation and one call for each execution");
        Assert.That(firstScenario.Steps[0].StepText, Is.EqualTo("When value: 1"));
        Assert.That(secondScenario.Steps[0].StepText, Is.EqualTo("When value: 1"));
    }

    [Test]
    public void ReportingAnUnexecutedStepDoesNotReevaluateItsArguments()
    {
        var source = new ArgumentSource();
        var builder = When(() => Fail()).Then(() => Display(source.GetValue()));

        Assert.ThrowsAsync<InvalidOperationException>(async () => await builder.BDTestAsync());

        Assert.That(source.Calls, Is.EqualTo(1));
        Assert.That(BDTestUtil.GetScenarios().Single().Steps[1].StepText, Is.EqualTo("Then value: 1"));
    }

    [Test]
    [BDTestRetry(1)]
    public async Task RetryingDoesNotReevaluateArgumentsForText()
    {
        var source = new ArgumentSource();
        var scenario = await When(() => source.FailOnce(source.GetValue()))
            .Then(() => Display("finished"))
            .BDTestAsync();

        Assert.That(scenario.RetryCount, Is.EqualTo(1));
        Assert.That(source.Calls, Is.EqualTo(3), "One text evaluation and two executions");
        Assert.That(scenario.Steps[0].StepText, Is.EqualTo("When value: 1"));
    }

    [TestCase("dynamic", "Then dynamic")]
    [TestCase("", "Then value: 1")]
    public async Task OverridesRemainDynamicAndEmptyOverridesUseCachedText(string updatedText, string expected)
    {
        var source = new ArgumentSource();
        var text = "initial";
        Action update = () => text = updatedText;
        var scenario = await When(() => SetText(update))
            .Then(() => Display(source.GetValue())).WithStepText(() => text)
            .BDTestAsync();

        Assert.That(source.Calls, Is.EqualTo(2));
        Assert.That(scenario.Steps[1].StepText, Is.EqualTo(expected));
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
