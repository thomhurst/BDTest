using System.Reflection;
using BDTest;
using BDTest.Helpers;
using BDTest.Interfaces;
using System.Threading.Tasks;

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
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            for (var i = 0; i < 500; i++)
            {
                await Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => value], run: first)).IsEqualTo("value: first");
                await Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => value], run: second)).IsEqualTo("value: first");
                await Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => value], run: unconfigured)).IsEqualTo("value: fallback");
            }
        })));
    }

    [Test]
    public async Task ConverterRegistrationRejectsNull() =>
        await Assert.That(() => new BDTestOptions().RegisterCustomStringConverter<Value>(null!)).ThrowsExactly<ArgumentNullException>();

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
            await Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => value], run: another)).IsEqualTo("value: fallback");
            resume.Set();
            await Assert.That(await conversion).IsEqualTo("value: active");
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
