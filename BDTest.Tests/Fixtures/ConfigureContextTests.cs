using BDTest.Attributes;
using BDTest.Exceptions;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ConfigureContextTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void ConfigurationCanReadTheContextBeingInitialized(bool frameworkId)
    {
        using var fixture = new ContextFixture(frameworkId);
        var calls = 0;
        BDTestContext<MyTestContext>? configured = null;
        fixture.Configure = context =>
        {
            // Fail safely on the old implementation instead of overflowing the test host's stack.
            Assert.That(++calls, Is.EqualTo(1), "The configuration must not recurse");
            configured = context;
            Assert.That(fixture.BDTestContext, Is.SameAs(context));
            Assert.That(fixture.Context, Is.SameAs(context.TestContext));
        };

        try
        {
            var initialized = fixture.BDTestContext;
            Assert.That(initialized, Is.SameAs(configured));
            Assert.That(fixture.Context, Is.SameAs(Required(configured).TestContext));
            Assert.That(calls, Is.EqualTo(1));
        }
        finally
        {
            fixture.Complete();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FailedConfigurationIsRethrownAndNextAccessCreatesFreshContext(bool frameworkId)
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
            Assert.That(Assert.Throws<InvalidOperationException>(() => _ = fixture.Context), Is.SameAs(expected));
            var recovered = fixture.BDTestContext;
            Assert.That(attempts.Count, Is.EqualTo(2));
            Assert.That(recovered, Is.SameAs(attempts[1]));
            Assert.That(recovered, Is.Not.SameAs(attempts[0]));
            Assert.That(recovered.TestContext, Is.Not.SameAs(attempts[0].TestContext));
            Assert.That(fixture.BDTestContext, Is.SameAs(recovered));
        }
        finally
        {
            fixture.Complete();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RetryCreatesAndConfiguresANewContextOnce(bool frameworkId)
    {
        using var fixture = new ContextFixture(frameworkId);
        var attempts = new List<BDTestContext<MyTestContext>>();
        fixture.Configure = context =>
        {
            attempts.Add(context);
            Assert.That(attempts.Count, Is.LessThanOrEqualTo(2), "The configuration must not recurse");
            Assert.That(fixture.BDTestContext, Is.SameAs(context));
            Assert.That(fixture.Context, Is.SameAs(context.TestContext));
        };

        try
        {
            var original = fixture.BDTestContext;
            var scenario = await fixture.RunRetriedScenario();
            Assert.That(scenario.RetryCount, Is.EqualTo(1));
            Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
            Assert.That(attempts.Count, Is.EqualTo(2));
            Assert.That(fixture.BDTestContext, Is.SameAs(attempts[1]));
            Assert.That(fixture.Context, Is.Not.SameAs(original.TestContext));
        }
        finally
        {
            fixture.Complete();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
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
            var error = await Assert.ThrowsAsync<ErrorOccurredDuringRetryActionException>(async () => await fixture.RunRetriedScenario());
            Assert.That(Required(error).GetBaseException(), Is.SameAs(expected));
            var recovered = fixture.BDTestContext;
            Assert.That(attempts.Count, Is.EqualTo(3));
            Assert.That(recovered, Is.SameAs(attempts[2]));
            Assert.That(recovered.TestContext, Is.Not.SameAs(original.TestContext));
            Assert.That(recovered.TestContext, Is.Not.SameAs(attempts[1].TestContext));
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
