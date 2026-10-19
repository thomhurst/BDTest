using System.Reflection;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Interfaces;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class CustomStringConverterTests
{
    private static readonly MethodInfo DisplayMethod = Required(typeof(CustomStringConverterTests)
        .GetMethod(nameof(Display), BindingFlags.Static | BindingFlags.NonPublic));

    [Test]
    public async Task ConcurrentFormattingUsesEachRunsFrozenConverters()
    {
        var options = new BDTestOptions();
        using var unconfigured = BDTestRun.Start(options);
        options.RegisterCustomStringConverter(new ValueConverter("first"));
        using var first = BDTestRun.Start(options);
        options.RegisterCustomStringConverter(new ValueConverter("second"));
        using var second = BDTestRun.Start(options);
        var value = new Value();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => value], run: first), Is.EqualTo("value: first"));
                Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => value], run: second), Is.EqualTo("value: first"));
                Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => value], run: unconfigured), Is.EqualTo("value: fallback"));
            }
        })));
    }

    [Test]
    public void ConverterRegistrationRejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => new BDTestOptions().RegisterCustomStringConverter<Value>(null!));

    [Test]
    public async Task ConfiguringAnotherRunDoesNotBlockAnActiveConverter()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resume = new ManualResetEventSlim();
        var options = new BDTestOptions();
        options.RegisterCustomStringConverter(new CallbackConverter(() =>
        {
            entered.SetResult();
            if (!resume.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
            return "active";
        }));
        using var run = BDTestRun.Start(options);
        var value = new Value();
        var conversion = Task.Run(() => StepTextHelper.GetStepText(DisplayMethod, [() => value], run: run));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            options.RegisterCustomStringConverter(new ValueConverter("next"));
            using var another = BDTestRun.Start();
            Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => value], run: another), Is.EqualTo("value: fallback"));
            resume.Set();
            Assert.That(await conversion, Is.EqualTo("value: active"));
        }
        finally { resume.Set(); await conversion; }
    }

    [StepText("value: {0}")]
    private static void Display(Value value) { }
    private sealed class Value { public override string ToString() => "fallback"; }
    private sealed class CallbackConverter(Func<string> callback) : IStepTextStringConverter<Value>
    { public string ConvertToString(Value value) => callback(); }
    private sealed class ValueConverter(string text) : IStepTextStringConverter<Value>
    { public string ConvertToString(Value value) => text; }
}
