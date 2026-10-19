using BDTest;
using BDTest.Helpers;
using BDTest.Test;
using BDTest.Tests.Helpers;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class StepErrorHandlingTests : RunTestBase
{
    [Test]
    [MatrixDataSource]
    public async Task NullStepNamesThePublicParameter([Matrix] EntryPoint entryPoint, [Matrix] bool asyncStep)
    {
        // Deliberately invalid delegates exercise the runtime null guards.
        Action action = null!;
        Func<Task> task = null!;

        var exception = await Assert.That(() =>
        {
            BuildableTest builder = entryPoint switch
            {
                EntryPoint.Given => asyncStep ? Given(task) : Given(action),
                EntryPoint.When => asyncStep ? When(task) : When(action),
                EntryPoint.GivenAnd => asyncStep ? Given(() => NoOp()).And(task) : Given(() => NoOp()).And(action),
                EntryPoint.GivenWhen => asyncStep ? Given(() => NoOp()).When(task) : Given(() => NoOp()).When(action),
                EntryPoint.AndGivenAnd => asyncStep ? Given(() => NoOp()).And(() => NoOp()).And(task) : Given(() => NoOp()).And(() => NoOp()).And(action),
                EntryPoint.AndGivenWhen => asyncStep ? Given(() => NoOp()).And(() => NoOp()).When(task) : Given(() => NoOp()).And(() => NoOp()).When(action),
                EntryPoint.WhenThen => asyncStep ? When(() => NoOp()).Then(task) : When(() => NoOp()).Then(action),
                EntryPoint.ThenAnd => asyncStep ? When(() => NoOp()).Then(() => NoOp()).And(task) : When(() => NoOp()).Then(() => NoOp()).And(action),
                EntryPoint.AndThenAnd => asyncStep ? When(() => NoOp()).Then(() => NoOp()).And(() => NoOp()).And(task) : When(() => NoOp()).Then(() => NoOp()).And(() => NoOp()).And(action),
                _ => throw new ArgumentOutOfRangeException(nameof(entryPoint))
            };
        }).ThrowsExactly<ArgumentNullException>();

        await Assert.That(Required(exception).ParamName).IsEqualTo("step");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InvalidStepTextIsDiagnosedWithoutThrowing(bool asyncStep)
    {
        var name = asyncStep ? nameof(InvalidTextAsync) : nameof(InvalidText);
        var method = Required(typeof(StepErrorHandlingTests).GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static));
        var diagnostics = new List<string>();
        var text = StepTextHelper.GetStepText(method, [() => "argument"], diagnostic: diagnostics.Add);
        await Assert.That(text).IsEqualTo("invalid {1}");
        await Assert.That(diagnostics.Single()).Contains("positional");
    }

#pragma warning disable BDT002 // These methods exercise runtime handling of invalid templates.
    [StepText("invalid {1}")]
    private static void InvalidText(string value) { }

    [StepText("invalid {1}")]
    private static Task InvalidTextAsync(string value) => Task.CompletedTask;
#pragma warning restore BDT002

    private static void NoOp() { }

    public enum EntryPoint
    {
        Given,
        When,
        GivenAnd,
        GivenWhen,
        AndGivenAnd,
        AndGivenWhen,
        WhenThen,
        ThenAnd,
        AndThenAnd
    }
}
