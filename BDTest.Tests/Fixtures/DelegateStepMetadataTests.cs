using System.Reflection;
using System.Threading;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class DelegateStepMetadataTests : RunTestBase
{
    private int _value;
    private int _reads;
    private int Value { get { _reads++; return _value; } }

    [SetUp]
    public void Setup()
    {

        _value = 0;
        _reads = 0;
    }

    [Test]
    public void GeneratedMemberReadersRemainLazyAndReadCurrentState()
    {
        var builder = When(() => Display(Value));
        var definition = Required(builder.ExistingSteps[0].Runnable).Definition;
        Assert.That(Required(definition.Method).Name, Is.EqualTo(nameof(Display)));
        Assert.That(definition.GetText(), Is.EqualTo("value: {value}"));
        Assert.That(_reads, Is.Zero);

        _value = 42;
        Assert.That(definition.GetText(true), Is.EqualTo("value: 42"));
        Assert.That(_reads, Is.EqualTo(1));
    }

    [Test]
    public void UnsupportedCapturedLocalUsesCallerTextUntilExplicitArgumentsAreProvided()
    {
        var value = 42;
        var builder = When(() => Display(value));
        var definition = Required(builder.ExistingSteps[0].Runnable).Definition;
        Assert.That(Required(definition.Method).Name, Is.EqualTo(nameof(Display)));
        Assert.That(definition.GetText(), Is.EqualTo("() => Display(value)"));
        Assert.That(definition.GetText(true), Is.EqualTo("() => Display(value)"));
        Assert.That(definition.GetText(explicitArguments: [() => value]), Is.EqualTo("value: {value}"));
        Assert.That(definition.GetText(true, explicitArguments: [() => value]), Is.EqualTo("value: 42"));
    }

    [Test]
    public void StatementBodyUsesMeaningfulCallerTextWithoutRunningTheDelegate()
    {
        var calls = 0;
        var builder = When(() => { calls++; Display(calls); });
        var definition = Required(builder.ExistingSteps[0].Runnable).Definition;
        Assert.That(definition.Method, Is.Null);
        Assert.That(definition.GetText(), Does.Contain("calls++").And.Contain("Display(calls)"));
        Assert.That(definition.GetText(true), Does.Contain("calls++"));
        Assert.That(calls, Is.Zero);
        Assert.That(definition.GetText(true, explicitArguments: [() => 42]), Does.EndWith(" 42"));
    }

    [Test]
    public void MethodGroupRetainsMethodTextWithoutGeneratedMetadata()
    {
        var definition = new StepTextDefinition((Action)SimpleMethodGroup, nameof(SimpleMethodGroup), "missing.cs", 1);
        Assert.That(Required(definition.Method).Name, Is.EqualTo(nameof(SimpleMethodGroup)));
        Assert.That(definition.GetText(), Is.EqualTo("simple method group"));
        Assert.That(definition.GetText(true), Is.EqualTo("simple method group"));
    }

    [Test]
    public void CancellationTokenReadersAreOmittedWithEitherExplicitArgumentShape()
    {
        var method = Required(typeof(DelegateStepMetadataTests).GetMethod(nameof(Cancellable), BindingFlags.Static | BindingFlags.NonPublic));
        Assert.That(StepTextHelper.GetStepText(method, [() => 42], omitCancellationToken: true), Is.EqualTo("value: 42"));
        Assert.That(StepTextHelper.GetStepText(method, [() => 42, () => throw new Exception("Token must not be rendered")],
            omitCancellationToken: true), Is.EqualTo("value: 42"));
    }

    [Test]
    public void ExplicitArgumentCountMismatchIsReported()
    {
        var method = Required(typeof(DelegateStepMetadataTests).GetMethod(nameof(Display), BindingFlags.Static | BindingFlags.NonPublic));
        var exception = Assert.Throws<ArgumentException>(() => StepTextHelper.GetStepText(method, []));
        Assert.That(Required(exception).ParamName, Is.EqualTo("arguments"));
        Assert.That(Required(exception).Message, Does.Contain("Expected 1 step text argument readers, but received 0"));
    }

    [StepText("value: {0}")]
    private static void Display(int value) { }

    [StepText("value: {0}")]
    private static Task Cancellable(int value, CancellationToken cancellationToken) => Task.CompletedTask;

    private static void SimpleMethodGroup() { }
}
