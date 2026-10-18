using System.Collections;
using System.Reflection;
using BDTest.Test;

namespace BDTest.TUnit.Tests;

[InheritsTests]
public class TUnitSameNamedHookTests : TUnitHookCollisionTests
{
    [Before(HookType.Test)]
    public new void Setup() => RecordSetup();

    [After(HookType.Test)]
    public new void TearDown() => RecordTearDown();
}

[InheritsTests]
public class TUnitOverloadedHookTests : TUnitHookCollisionTests
{
    [Before(HookType.Test)]
    public void Setup(TestContext context) => RecordSetup();

    [After(HookType.Test)]
    public void TearDown(TestContext context) => RecordTearDown();
}

public abstract class TUnitHookCollisionTests : TUnitBDTestBase<TUnitContext>
{
    private int _setupCalls;
    private int _tearDownCalls;
    private string _testId;

    protected void RecordSetup()
    {
        _setupCalls++;
        _testId = TestContext.Current!.Id;
        _ = Context;
        Console.WriteLine($"setup:{_testId}");
    }

    protected void RecordTearDown()
    {
        _tearDownCalls++;
        WriteTearDownOutput($"teardown:{_testId}");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task UserHooksDoNotReplaceAdapterLifecycle(bool runScenario)
    {
        await Assert.That(_setupCalls).IsEqualTo(1);
        await Assert.That(_tearDownCalls).IsEqualTo(0);
        await Assert.That(BDTestContext.CurrentScenarioBDTestExecutionId).IsEqualTo(_testId);
        await Task.Yield();

        if (runScenario)
        {
            var scenario = await Given(() => Console.WriteLine("step"))
                .When(() => NoOp())
                .Then(() => NoOp())
                .BDTestAsync();

            await Assert.That(scenario.TestStartupInformation)
                .IsEqualTo($"setup:{_testId}{Environment.NewLine}");
        }
        else
        {
            // Completion must also clear pending output and listeners when no scenario runs.
            WriteStartupOutput("pending output");
            ScenarioHtmlWriter.Custom("pending HTML");
        }
    }

    internal async Task VerifyCleanup()
    {
        await Assert.That(_setupCalls).IsEqualTo(1);
        await Assert.That(_tearDownCalls).IsEqualTo(1);
        var contexts = (IDictionary)typeof(AbstractContextBDTestBase<TUnitContext>)
            .GetField("_contexts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(this)!;
        await Assert.That(contexts.Count).IsEqualTo(0);

        var store = typeof(BDTestRun).GetProperty("Store", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(TestRun)!;
        var gate = store.GetType().GetProperty("Gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!;
        bool hasPendingOutput;
        bool hasPendingListener;
        lock (gate)
        {
            hasPendingOutput = ((IDictionary)GetField(store, "PendingStartupOutput")).Contains(_testId);
            hasPendingListener = ((IDictionary)GetField(store, "ScenarioListeners")).Contains(_testId);
        }

        await Assert.That(hasPendingOutput).IsFalse();
        await Assert.That(hasPendingListener).IsFalse();
        var scenario = BDTestUtil.GetScenarios().SingleOrDefault(s => s.FrameworkTestId == _testId);
        if (scenario != null)
        {
            await Assert.That(scenario.TearDownOutput)
                .IsEqualTo($"teardown:{_testId}{Environment.NewLine}");
        }
    }

    private static void NoOp() { }

    private static object GetField(object instance, string name) =>
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
}

public static class TUnitHookCollisionCleanup
{
    // Global after hooks run after all instance hooks, including the adapter cleanup.
    [AfterEvery(HookType.Test)]
    public static async Task VerifyAdapterCleanup(TestContext context)
    {
        if (context.Metadata.TestDetails.Class.ClassInstance is TUnitHookCollisionTests fixture)
        {
            await fixture.VerifyCleanup();
        }
    }
}
