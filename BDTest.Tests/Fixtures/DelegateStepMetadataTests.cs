using System.Reflection;
using System.Threading;
using BDTest;
using BDTest.Helpers;
using BDTest.Test;
using BDTest.Tests.Helpers;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class DelegateStepMetadataTests : RunTestBase
{
    private int _value;
    private int _reads;
    private int Value { get { _reads++; return _value; } }

    [Before(HookType.Test)]
    public void Setup()
    {

        _value = 0;
        _reads = 0;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("TUnit", "TUnit0018", Justification = "Each TUnit case owns this fixture; scenario callbacks and teardown intentionally observe its state.")]
    [Test]
    public async Task GeneratedMemberReadersRemainLazyAndReadCurrentState()
    {
        var builder = When(() => Display(Value));
        var definition = Required(builder.ExistingSteps[0].Runnable).Definition;
        await Assert.That(Required(definition.Method).Name).IsEqualTo(nameof(Display));
        await Assert.That(definition.GetText()).IsEqualTo("value: {value}");
        await Assert.That(_reads).IsZero();

        _value = 42;
        await Assert.That(definition.GetText(true)).IsEqualTo("value: 42");
        await Assert.That(_reads).IsEqualTo(1);
    }

    [Test]
    public async Task UnsupportedCapturedLocalUsesCallerTextUntilExplicitArgumentsAreProvided()
    {
        var value = 42;
        var builder = When(() => Display(value));
        var definition = Required(builder.ExistingSteps[0].Runnable).Definition;
        await Assert.That(Required(definition.Method).Name).IsEqualTo(nameof(Display));
        await Assert.That(definition.GetText()).IsEqualTo("() => Display(value)");
        await Assert.That(definition.GetText(true)).IsEqualTo("() => Display(value)");
        await Assert.That(definition.GetText(explicitArguments: [() => value])).IsEqualTo("value: {value}");
        await Assert.That(definition.GetText(true, explicitArguments: [() => value])).IsEqualTo("value: 42");
    }

    [Test]
    public async Task StatementBodyUsesMeaningfulCallerTextWithoutRunningTheDelegate()
    {
        var calls = 0;
        var builder = When(() => { calls++; Display(calls); });
        var definition = Required(builder.ExistingSteps[0].Runnable).Definition;
        await Assert.That(definition.Method).IsNull();

        await Assert.That(definition.GetText()).Contains("calls++").And.Contains("Display(calls)");
        await Assert.That(definition.GetText(true)).Contains("calls++");
        await Assert.That(calls).IsZero();
        await Assert.That(definition.GetText(true, explicitArguments: [() => 42])).EndsWith(" 42");
    }

    [Test]
    public async Task MethodGroupRetainsMethodTextWithoutGeneratedMetadata()
    {
        var definition = new StepTextDefinition((Action)SimpleMethodGroup, nameof(SimpleMethodGroup), "missing.cs", 1);
        await Assert.That(Required(definition.Method).Name).IsEqualTo(nameof(SimpleMethodGroup));
        await Assert.That(definition.GetText()).IsEqualTo("simple method group");
        await Assert.That(definition.GetText(true)).IsEqualTo("simple method group");
    }

    [Test]
    public async Task CancellationTokenReadersAreOmittedWithEitherExplicitArgumentShape()
    {
        var method = Required(typeof(DelegateStepMetadataTests).GetMethod(nameof(Cancellable), BindingFlags.Static | BindingFlags.NonPublic));
        await Assert.That(StepTextHelper.GetStepText(method, [() => 42], omitCancellationToken: true)).IsEqualTo("value: 42");
        await Assert.That(StepTextHelper.GetStepText(method, [() => 42, () => throw new Exception("Token must not be rendered")],
            omitCancellationToken: true)).IsEqualTo("value: 42");
    }

    [Test]
    public async Task ExplicitArgumentCountMismatchIsReported()
    {
        var method = Required(typeof(DelegateStepMetadataTests).GetMethod(nameof(Display), BindingFlags.Static | BindingFlags.NonPublic));
        var exception = await Assert.That(() => StepTextHelper.GetStepText(method, [])).ThrowsExactly<ArgumentException>();
        await Assert.That(Required(exception).ParamName).IsEqualTo("arguments");
        await Assert.That(Required(exception).Message).Contains("Expected 1 step text argument readers, but received 0");
    }

    [StepText("value: {value}")]
    private static void Display(int value) { }

    [StepText("value: {value}")]
    private static Task Cancellable(int value, CancellationToken cancellationToken) => Task.CompletedTask;

    private static void SimpleMethodGroup() { }
}
