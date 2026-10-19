using BDTest.Attributes;
using BDTest.Exceptions;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class ConfigureContextTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ConfigurationCanReadTheContextBeingInitialized(bool frameworkId)
    {
        using var fixture = new ContextFixture(frameworkId);
        var calls = 0;
        BDTestContext<MyTestContext>? configured = null;
        fixture.Configure = context =>
        {
            // Fail safely on the old implementation instead of overflowing the test host's stack.
            RequireCondition(++calls == 1, "The configuration must not recurse");
            configured = context;
            RequireCondition(ReferenceEquals(fixture.BDTestContext, context), "Configuration must see the current BDTest context");
            RequireCondition(ReferenceEquals(fixture.Context, context.TestContext), "Configuration must see the current test context");
        };

        try
        {
            var initialized = fixture.BDTestContext;
            await Assert.That(initialized).IsSameReferenceAs(configured);
            await Assert.That(fixture.Context).IsSameReferenceAs(Required(configured).TestContext);
            await Assert.That(calls).IsEqualTo(1);
        }
        finally
        {
            fixture.Complete();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FailedConfigurationIsRethrownAndNextAccessCreatesFreshContext(bool frameworkId)
    {
        using var fixture = new ContextFixture(frameworkId);
        var expected = new InvalidOperationException("configuration failed");
        var attempts = new List<BDTestContext<MyTestContext>>();
        fixture.Configure = context =>
        {
            attempts.Add(context);
            if (attempts.Count == 1)
            {
                throw expected;
            }
        };

        try
        {
            await Assert.That(await Assert.That(() => _ = fixture.Context).ThrowsExactly<InvalidOperationException>()).IsSameReferenceAs(expected);
            var recovered = fixture.BDTestContext;
            await Assert.That(attempts.Count).IsEqualTo(2);
            await Assert.That(recovered).IsSameReferenceAs(attempts[1]);
            await Assert.That(recovered).IsNotSameReferenceAs(attempts[0]);
            await Assert.That(recovered.TestContext).IsNotSameReferenceAs(attempts[0].TestContext);
            await Assert.That(fixture.BDTestContext).IsSameReferenceAs(recovered);
        }
        finally
        {
            fixture.Complete();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RetryCreatesAndConfiguresANewContextOnce(bool frameworkId)
    {
        using var fixture = new ContextFixture(frameworkId);
        var attempts = new List<BDTestContext<MyTestContext>>();
        fixture.Configure = context =>
        {
            attempts.Add(context);
            RequireCondition(attempts.Count <= 2, "The configuration must not recurse");
            RequireCondition(ReferenceEquals(fixture.BDTestContext, context), "Configuration must see the current BDTest context");
            RequireCondition(ReferenceEquals(fixture.Context, context.TestContext), "Configuration must see the current test context");
        };

        try
        {
            var original = fixture.BDTestContext;
            var scenario = await fixture.RunRetriedScenario();
            await Assert.That(scenario.RetryCount).IsEqualTo(1);
            await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
            await Assert.That(attempts.Count).IsEqualTo(2);
            await Assert.That(fixture.BDTestContext).IsSameReferenceAs(attempts[1]);
            await Assert.That(fixture.Context).IsNotSameReferenceAs(original.TestContext);
        }
        finally
        {
            fixture.Complete();
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FailedRetryConfigurationDoesNotRetainPartiallyInitializedContext(bool frameworkId)
    {
        using var fixture = new ContextFixture(frameworkId);
        var expected = new InvalidOperationException("retry configuration failed");
        var attempts = new List<BDTestContext<MyTestContext>>();
        fixture.Configure = context =>
        {
            attempts.Add(context);
            if (attempts.Count == 2)
            {
                throw expected;
            }
        };

        try
        {
            var original = fixture.BDTestContext;
            var error = await Assert.That(async () => await fixture.RunRetriedScenario()).ThrowsExactly<ErrorOccurredDuringRetryActionException>();
            await Assert.That(Required(error).GetBaseException()).IsSameReferenceAs(expected);
            var recovered = fixture.BDTestContext;
            await Assert.That(attempts.Count).IsEqualTo(3);
            await Assert.That(recovered).IsSameReferenceAs(attempts[2]);
            await Assert.That(recovered.TestContext).IsNotSameReferenceAs(original.TestContext);
            await Assert.That(recovered.TestContext).IsNotSameReferenceAs(attempts[1].TestContext);
        }
        finally
        {
            fixture.Complete();
        }
    }

    [BDTestRetry(1)]
    private sealed class ContextFixture(bool frameworkId) : RunContextTestBase<MyTestContext>
    {
        private readonly string _frameworkId = Guid.NewGuid().ToString();
        protected override string BDTestExecutionId => frameworkId ? _frameworkId : base.BDTestExecutionId;

        public Action<BDTestContext<MyTestContext>>? Configure { get; set; }
        protected override void ConfigureContext(BDTestContext<MyTestContext> context) => Configure?.Invoke(context);

        private int _attempts;

        public async Task<Scenario> RunRetriedScenario() => await Given(() => ReadContext())
            .When(() => FailFirstAttempt())
            .Then(() => ReadContext())
            .BDTestAsync();

        private void ReadContext() => _ = Context;

        private void FailFirstAttempt()
        {
            if (++_attempts == 1)
            {
                throw new InvalidOperationException("retry this scenario");
            }
        }

        // Exercise completion of standalone contexts that never called BeginTest.
        public void Complete() => BDTest.Tests.Helpers.LegacyLifecycle.Complete(this);
    }
}
