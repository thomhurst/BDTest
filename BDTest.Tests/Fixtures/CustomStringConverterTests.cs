using System.Reflection;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Interfaces;
using BDTest.Settings;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class CustomStringConverterTests
{
    private static readonly MethodInfo DisplayMethod = Required(typeof(CustomStringConverterTests)
        .GetMethod(nameof(Display), BindingFlags.Static | BindingFlags.NonPublic));

    [TestCase(false)]
    [TestCase(true)]
    public async Task ConcurrentRegistrationAndRemovalDoNotLoseStepText(bool legacyAccess)
    {
        var fillers = Enumerable.Range(0, 64).Select(_ => new NumberConverter()).ToArray();
        var converter = new ValueConverter("converted");
        var changing = new NumberConverter();
        var value = new Value();
        Func<object>[] arguments = [() => value];
        using var start = new ManualResetEventSlim();
        using var stop = new CancellationTokenSource();

        lock (BDTestSettings.CustomStringConverters)
        {
            BDTestSettings.CustomStringConverters.AddRange(fillers);
            BDTestSettings.CustomStringConverters.Add(converter);
        }

        var writer = Task.Run(() =>
        {
            start.Wait();
            while (!stop.IsCancellationRequested)
            {
                if (legacyAccess)
                {
                    lock (BDTestSettings.CustomStringConverters)
                    {
                        BDTestSettings.CustomStringConverters.Add(changing);
                        BDTestSettings.CustomStringConverters.Remove(changing);
                    }
                }
                else
                {
                    BDTestSettings.RegisterCustomStringConverter(changing);
                    BDTestSettings.RemoveCustomStringConverter(changing);
                }
                Thread.Yield();
            }
        });
        var readers = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            start.Wait();
            for (var i = 0; i < 500; i++)
            {
                Assert.That(StepTextHelper.GetStepText(DisplayMethod, arguments), Is.EqualTo("value: converted"));
            }
        })).ToArray();

        start.Set();
        try
        {
            await Task.WhenAll(readers);
        }
        finally
        {
            stop.Cancel();
            await writer;
            lock (BDTestSettings.CustomStringConverters)
            {
                foreach (var filler in fillers)
                {
                    BDTestSettings.CustomStringConverters.Remove(filler);
                }
                BDTestSettings.CustomStringConverters.Remove(converter);
                BDTestSettings.CustomStringConverters.Remove(changing);
            }
        }
    }

    [Test]
    public void RegistrationOrderAndDuplicateRemovalPreserveFirstMatch()
    {
        var first = new ValueConverter("first");
        var second = new ValueConverter("second");
        var value = new Value();
        BDTestSettings.RegisterCustomStringConverter(first);
        BDTestSettings.RegisterCustomStringConverter(second);
        BDTestSettings.RegisterCustomStringConverter(first);
        try
        {
            Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => value]), Is.EqualTo("value: first"));
            Assert.That(BDTestSettings.RemoveCustomStringConverter(first), Is.True);
            Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => value]), Is.EqualTo("value: second"));
            Assert.That(BDTestSettings.RemoveCustomStringConverter(second), Is.True);
            Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => value]), Is.EqualTo("value: first"));
            Assert.That(BDTestSettings.RemoveCustomStringConverter(first), Is.True);
            Assert.That(BDTestSettings.RemoveCustomStringConverter(first), Is.False);
        }
        finally
        {
            BDTestSettings.RemoveCustomStringConverter(first);
            BDTestSettings.RemoveCustomStringConverter(first);
            BDTestSettings.RemoveCustomStringConverter(second);
        }
    }

    [Test]
    public void SnapshotRetainsRegistrationOrderAfterRemovalAndRegistration()
    {
        var first = new ValueConverter("first");
        var second = new ValueConverter("second");
        BDTestSettings.RegisterCustomStringConverter(first);
        try
        {
            var snapshot = BDTestSettings.GetCustomStringConvertersSnapshot();
            BDTestSettings.RemoveCustomStringConverter(first);
            BDTestSettings.RegisterCustomStringConverter(second);

            Assert.That(snapshot, Does.Contain(first));
            Assert.That(snapshot, Does.Not.Contain(second));
            var value = new Value();
            Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => value]), Is.EqualTo("value: second"));
        }
        finally
        {
            BDTestSettings.RemoveCustomStringConverter(first);
            BDTestSettings.RemoveCustomStringConverter(second);
        }
    }

    [Test]
    public async Task RemovingConverterDoesNotBlockOrCancelItsActiveCallback()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resume = new ManualResetEventSlim();
        var converter = new CallbackConverter(() =>
        {
            entered.SetResult();
            if (!resume.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("Converter callback was not resumed");
            }
            return "active";
        });
        var value = new Value();
        BDTestSettings.RegisterCustomStringConverter(converter);
        var conversion = Task.Run(() => StepTextHelper.GetStepText(DisplayMethod, [() => value]));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(await Task.Run(() => BDTestSettings.RemoveCustomStringConverter(converter))
                .WaitAsync(TimeSpan.FromSeconds(5)), Is.True);
            resume.Set();
            Assert.That(await conversion, Is.EqualTo("value: active"));
            Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => value]), Is.EqualTo("value: fallback"));
        }
        finally
        {
            resume.Set();
            await conversion;
            BDTestSettings.RemoveCustomStringConverter(converter);
        }
    }

    [Test]
    public void LegacySetupTimeRegistrationStillUsesShippedListProperty()
    {
        List<object> converters = BDTestSettings.CustomStringConverters;
        var converter = new ValueConverter("legacy");
        converters.Add(converter);
        try
        {
            var value = new Value();
            Assert.That(StepTextHelper.GetStepText(DisplayMethod, [() => value]), Is.EqualTo("value: legacy"));
        }
        finally
        {
            converters.Remove(converter);
        }
    }

    [Test]
    public void RegistrationAndRemovalRejectNull()
    {
        Assert.Throws<ArgumentNullException>(() => BDTestSettings.RegisterCustomStringConverter<Value>(null));
        Assert.Throws<ArgumentNullException>(() => BDTestSettings.RemoveCustomStringConverter<Value>(null));
    }

    [StepText("value: {0}")]
    private static void Display(Value value) { }

    private sealed class Value
    {
        public override string ToString() => "fallback";
    }

    private sealed class CallbackConverter(Func<string> callback) : IStepTextStringConverter<Value>
    {
        public string ConvertToString(Value value) => callback();
    }

    private sealed class ValueConverter(string text) : IStepTextStringConverter<Value>
    {
        public string ConvertToString(Value value) => text;
    }

    private sealed class NumberConverter : IStepTextStringConverter<int>
    {
        public string ConvertToString(int value) => value.ToString();
    }
}
