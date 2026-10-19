using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class DefaultExecutionIdTests : TestRunOwner
{
    [Test]
    public async Task BuilderKeepsStartupOutputFromAsyncSetup()
    {
        var builder = new BDTestBuilder(TestRun);
        await Task.Run(() => builder.WriteStartupOutput("async startup"));

        var scenario = await builder.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        await Assert.That(scenario.TestStartupInformation).Contains("async startup");
    }

    [Test]
    public async Task BuildersHaveIndependentIdsInTheSameFlow()
    {
        var first = new BDTestBuilder(TestRun);
        var second = new BDTestBuilder(TestRun);
        first.WriteStartupOutput("first startup");
        second.WriteStartupOutput("second startup");

        var firstScenario = await first.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        var secondScenario = await second.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        await Assert.That(firstScenario.FrameworkTestId).IsNotEqualTo(secondScenario.FrameworkTestId);

        await Assert.That(firstScenario.TestStartupInformation).Contains("first startup").And.DoesNotContain("second startup");

        await Assert.That(secondScenario.TestStartupInformation).Contains("second startup").And.DoesNotContain("first startup");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ExplicitStartKeepsContextAndOutputAcrossAsyncSetup(bool useTaskRun)
    {
        using var fixture = new ContextFixture();
        fixture.Start();
        try
        {
            var setupContext = useTaskRun
                ? await Task.Run(fixture.SetupAsync)
                : await fixture.SetupAsync();

            await Assert.That(fixture.Context).IsSameReferenceAs(setupContext.TestContext);
            await Assert.That(fixture.BDTestContext.CurrentScenarioBDTestExecutionId).IsEqualTo(setupContext.CurrentScenarioBDTestExecutionId);
            var scenario = await fixture.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
            await Assert.That(scenario.TestStartupInformation).Contains("context startup");
            fixture.WriteTearDownOutput("context teardown");
            await Assert.That(fixture.TestRun.Scenarios.Single(s => s.Guid == scenario.Guid).TearDownOutput).Contains("context teardown");
        }
        finally
        {
            fixture.Complete();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task StartsIsolateConcurrentExecutionsOnSharedFixture(bool useLegacyStart)
    {
        using var fixture = new ContextFixture();
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        var executions = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
        {
            fixture.Start(useLegacyStart);
            try
            {
                var context = fixture.BDTestContext;
                if (Interlocked.Increment(ref count) == 8)
                {
                    ready.SetResult(true);
                }
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
                await Assert.That(fixture.BDTestContext).IsSameReferenceAs(context);
                return context;
            }
            finally
            {
                fixture.Complete();
            }
        })));

        await Assert.That(executions.Select(context => context.CurrentScenarioBDTestExecutionId).Distinct().Count()).IsEqualTo(8);
        await Assert.That(executions.Select(context => context.TestContext).Distinct().Count()).IsEqualTo(8);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task StartAfterCompletionCreatesANewExecution(bool useLegacyStart)
    {
        using var fixture = new ContextFixture();
        fixture.Start(useLegacyStart);
        var first = fixture.BDTestContext;
        fixture.Complete();
        fixture.Start(useLegacyStart);
        try
        {
            await Assert.That(fixture.Context).IsNotSameReferenceAs(first.TestContext);
            await Assert.That(fixture.BDTestContext.CurrentScenarioBDTestExecutionId).IsNotEqualTo(first.CurrentScenarioBDTestExecutionId);
        }
        finally
        {
            fixture.Complete();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AsyncEntryPointPreservesCallerIdentity(bool fail)
    {
        using var fixture = new ContextFixture();
        fixture.Start();
        var callerContext = fixture.BDTestContext;
        var callerOutputId = BDTest.Output.TestOutputData.FrameworkExecutionId;
        try
        {
            try
            {
                await fixture.RunTestAsync(fail);
                await Assert.That(fail).IsFalse();
            }
            catch (InvalidOperationException) when (fail)
            {
            }

            await Assert.That(fixture.BDTestContext).IsSameReferenceAs(callerContext);
            await Assert.That(BDTest.Output.TestOutputData.FrameworkExecutionId).IsEqualTo(callerOutputId);
        }
        finally
        {
            fixture.Complete();
        }
    }

    private sealed class ContextFixture : RunContextTestBase<MyTestContext>
    {
        public void Start(bool useLegacyStart = false)
        {
            if (useLegacyStart)
            {
                Required(typeof(BDTestBase).GetMethod("MarkTestAsStarted",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
                    .CreateDelegate<Action>(this)();
            }
            else
            {
                BeginTest(Guid.NewGuid().ToString("N"));
            }
        }
        public void Complete() => EndTest();

        public async Task RunTestAsync(bool fail)
        {
            BeginTest(Guid.NewGuid().ToString("N"));
            try
            {
                await SetupAsync();
                if (fail)
                {
                    throw new InvalidOperationException("setup failed");
                }
                await When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
            }
            finally
            {
                EndTest();
            }
        }

        public async Task<BDTestContext<MyTestContext>> SetupAsync()
        {
            await Task.Yield();
            WriteStartupOutput("context startup");
            return BDTestContext;
        }
    }

    private static void NoOp() { }
}
