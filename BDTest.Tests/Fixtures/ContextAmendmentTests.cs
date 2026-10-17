using BDTest.Attributes;
using BDTest.Exceptions;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ContextAmendmentTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void AmendmentCanReadTheContextBeingInitialized(bool frameworkId)
    {
        var fixture = new ContextFixture(frameworkId);
        var calls = 0;
        BDTestContext<MyTestContext> amended = null;
        fixture.ContextAmendment = context =>
        {
            // Fail safely on the old implementation instead of overflowing the test host's stack.
            Assert.That(++calls, Is.EqualTo(1), "The amendment must not recurse");
            amended = context;
            Assert.That(fixture.BDTestContext, Is.SameAs(context));
            Assert.That(fixture.Context, Is.SameAs(context.TestContext));
        };

        try
        {
            var initialized = fixture.BDTestContext;
            Assert.That(initialized, Is.SameAs(amended));
            Assert.That(fixture.Context, Is.SameAs(amended.TestContext));
            Assert.That(calls, Is.EqualTo(1));
        }
        finally
        {
            fixture.Complete();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FailedAmendmentIsRethrownAndNextAccessCreatesFreshContext(bool frameworkId)
    {
        var fixture = new ContextFixture(frameworkId);
        var expected = new InvalidOperationException("amendment failed");
        var attempts = new List<BDTestContext<MyTestContext>>();
        fixture.ContextAmendment = context =>
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
    public void RetryCreatesAndAmendsANewContextOnce(bool frameworkId)
    {
        var fixture = new ContextFixture(frameworkId);
        var attempts = new List<BDTestContext<MyTestContext>>();
        fixture.ContextAmendment = context =>
        {
            attempts.Add(context);
            Assert.That(attempts.Count, Is.LessThanOrEqualTo(2), "The amendment must not recurse");
            Assert.That(fixture.BDTestContext, Is.SameAs(context));
            Assert.That(fixture.Context, Is.SameAs(context.TestContext));
        };

        try
        {
            var original = fixture.BDTestContext;
            var scenario = fixture.RunRetriedScenario();
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
    public void FailedRetryAmendmentDoesNotRetainPartiallyInitializedContext(bool frameworkId)
    {
        var fixture = new ContextFixture(frameworkId);
        var expected = new InvalidOperationException("retry amendment failed");
        var attempts = new List<BDTestContext<MyTestContext>>();
        fixture.ContextAmendment = context =>
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
            var error = Assert.Throws<ErrorOccurredDuringRetryActionException>(() => fixture.RunRetriedScenario());
            Assert.That(error.GetBaseException(), Is.SameAs(expected));
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
    private sealed class ContextFixture(bool frameworkId) : AbstractContextBDTestBase<MyTestContext>
    {
        private readonly string _frameworkId = Guid.NewGuid().ToString();
        protected override string BDTestExecutionId => frameworkId ? _frameworkId : base.BDTestExecutionId;

        private int _attempts;

        public Scenario RunRetriedScenario() => Given(() => ReadContext())
            .When(() => FailFirstAttempt())
            .Then(() => ReadContext())
            .BDTest();

        private void ReadContext() => _ = Context;

        private void FailFirstAttempt()
        {
            if (++_attempts == 1)
            {
                throw new InvalidOperationException("retry this scenario");
            }
        }

        public void Complete() => MarkTestAsComplete();
    }
}
