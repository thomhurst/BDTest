using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class DefaultExecutionIdTests
{
    [Test]
    public async Task BuilderKeepsStartupOutputFromAsyncSetup()
    {
        var builder = new BDTestBuilder();
        await Task.Run(() => builder.WriteStartupOutput("async startup"));

        var scenario = await builder.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        Assert.That(scenario.TestStartupInformation, Does.Contain("async startup"));
    }

    [Test]
    public async Task BuildersHaveIndependentIdsInTheSameFlow()
    {
        var first = new BDTestBuilder();
        var second = new BDTestBuilder();
        first.WriteStartupOutput("first startup");
        second.WriteStartupOutput("second startup");

        var firstScenario = await first.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        var secondScenario = await second.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        Assert.That(firstScenario.FrameworkTestId, Is.Not.EqualTo(secondScenario.FrameworkTestId));
        Assert.That(firstScenario.TestStartupInformation, Does.Contain("first startup").And.Not.Contain("second startup"));
        Assert.That(secondScenario.TestStartupInformation, Does.Contain("second startup").And.Not.Contain("first startup"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ExplicitStartKeepsContextAndOutputAcrossAsyncSetup(bool useTaskRun)
    {
        var fixture = new ContextFixture();
        fixture.Start();
        try
        {
            var setupContext = useTaskRun
                ? await Task.Run(fixture.SetupAsync)
                : await fixture.SetupAsync();

            Assert.That(fixture.Context, Is.SameAs(setupContext.TestContext));
            Assert.That(fixture.BDTestContext.CurrentScenarioBDTestExecutionId,
                Is.EqualTo(setupContext.CurrentScenarioBDTestExecutionId));
            var scenario = await fixture.When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
            Assert.That(scenario.TestStartupInformation, Does.Contain("context startup"));
            fixture.WriteTearDownOutput("context teardown");
            Assert.That(scenario.TearDownOutput, Does.Contain("context teardown"));
        }
        finally
        {
            fixture.Complete();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task StartsIsolateConcurrentExecutionsOnSharedFixture(bool useLegacyStart)
    {
        var fixture = new ContextFixture();
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
                Assert.That(fixture.BDTestContext, Is.SameAs(context));
                return context;
            }
            finally
            {
                fixture.Complete();
            }
        })));

        Assert.That(executions.Select(context => context.CurrentScenarioBDTestExecutionId).Distinct().Count(), Is.EqualTo(8));
        Assert.That(executions.Select(context => context.TestContext).Distinct().Count(), Is.EqualTo(8));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StartAfterCompletionCreatesANewExecution(bool useLegacyStart)
    {
        var fixture = new ContextFixture();
        fixture.Start(useLegacyStart);
        var first = fixture.BDTestContext;
        fixture.Complete();
        fixture.Start(useLegacyStart);
        try
        {
            Assert.That(fixture.Context, Is.Not.SameAs(first.TestContext));
            Assert.That(fixture.BDTestContext.CurrentScenarioBDTestExecutionId,
                Is.Not.EqualTo(first.CurrentScenarioBDTestExecutionId));
        }
        finally
        {
            fixture.Complete();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AsyncEntryPointPreservesCallerIdentity(bool fail)
    {
        var fixture = new ContextFixture();
        fixture.Start();
        var callerContext = fixture.BDTestContext;
        var callerOutputId = BDTest.Output.TestOutputData.FrameworkExecutionId;
        try
        {
            try
            {
                await fixture.RunTestAsync(fail);
                Assert.That(fail, Is.False);
            }
            catch (InvalidOperationException) when (fail)
            {
            }

            Assert.That(fixture.BDTestContext, Is.SameAs(callerContext));
            Assert.That(BDTest.Output.TestOutputData.FrameworkExecutionId, Is.EqualTo(callerOutputId));
        }
        finally
        {
            fixture.Complete();
        }
    }

    private sealed class ContextFixture : AbstractContextBDTestBase<MyTestContext>
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
