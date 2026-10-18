using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class StepErrorHandlingTests : BDTestBase
{
    [SetUp]
    public void Setup() => TestResetHelper.ResetData();

    [TearDown]
    public void Cleanup() => TestResetHelper.ResetData();

    [Test]
    public void NullStepNamesThePublicParameter([Values] EntryPoint entryPoint, [Values] bool asyncStep)
    {
        Action action = null;
        Func<Task> task = null;

        var exception = Assert.Throws<ArgumentNullException>(() =>
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
        });

        Assert.That(exception.ParamName, Is.EqualTo("step"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InvalidStepTextPreservesTheOriginalException(bool asyncStep)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            if (asyncStep)
            {
                StepTextHelper.GetStepText(typeof(StepErrorHandlingTests).GetMethod(nameof(InvalidTextAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static), new Func<object>[] { () => "argument" });
            }
            else
            {
                StepTextHelper.GetStepText(typeof(StepErrorHandlingTests).GetMethod(nameof(InvalidText), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static), new Func<object>[] { () => "argument" });
            }
        });

        Assert.That(exception.Message, Does.Contain("Template is: invalid {1}"));
        Assert.That(exception.Message, Does.Contain("Arguments are argument"));
        Assert.That(exception.InnerException, Is.TypeOf<FormatException>());
        Assert.That(exception.InnerException.StackTrace, Does.Contain(nameof(StepTextHelper)));
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
