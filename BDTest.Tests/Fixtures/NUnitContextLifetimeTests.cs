using BDTest.Attributes;
using BDTest.NUnit;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[Parallelizable(ParallelScope.All)]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class NUnitContextLifetimeTests : NUnitBDTestBase<MyTestContext>
{
    [Test]
    public async Task ContextStateIsSharedAcrossSteps()
    {
        await Given(() => SetContextId("set in given"))
            .When(() => YieldAsync())
            .Then(() => AssertContextId("set in given"))
            .BDTestAsync();
    }

    [Test]
    public async Task ConcurrentAccessCreatesAndAmendsOneContext()
    {
        var amendmentCount = 0;
        ContextAmendment = context =>
        {
            Interlocked.Increment(ref amendmentCount);
            context.TestContext.Id = "amended";
        };

        var contexts = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => BDTestContext)));

        Assert.That(contexts.All(context => ReferenceEquals(context, contexts[0])), Is.True);
        Assert.That(Context, Is.SameAs(contexts[0].TestContext));
        Assert.That(Context.Id, Is.EqualTo("amended"));
        Assert.That(amendmentCount, Is.EqualTo(1));
    }

    [Test]
    public void OverwriteContextReplacesTheStoredContext()
    {
        var original = BDTestContext;
        var replacement = new MyTestContext();

        OverwriteContext(replacement);

        Assert.That(Context, Is.SameAs(replacement));
        Assert.That(BDTestContext, Is.Not.SameAs(original));
        Assert.That(BDTestContext, Is.SameAs(BDTestContext));
    }

    [Test]
    public void TeardownRemovesTheStoredContext()
    {
        var amendmentCount = 0;
        ContextAmendment = _ => amendmentCount++;
        var original = BDTestContext;

        PruneContext();

        var next = BDTestContext;
        Assert.That(next, Is.Not.SameAs(original));
        Assert.That(next.TestContext, Is.Not.SameAs(original.TestContext));
        Assert.That(BDTestContext, Is.SameAs(next));
        Assert.That(amendmentCount, Is.EqualTo(2));
    }

    [Test, BDTestRetry(1)]
    public async Task RetryCreatesAndAmendsAFreshContext()
    {
        var amendmentCount = 0;
        ContextAmendment = context =>
        {
            amendmentCount++;
            context.TestContext.Id = "amended";
        };
        var attempts = new List<MyTestContext>();

        await Given(() => RecordAttempt(attempts))
            .When(() => FailFirstAttempt(attempts))
            .Then(() => VerifyRetryContext(attempts, amendmentCount))
            .BDTestAsync();

    }

    private void VerifyRetryContext(List<MyTestContext> attempts, int amendmentCount)
    {
        AssertContextId("changed in given");
        Assert.That(attempts, Has.Count.EqualTo(2));
        Assert.That(attempts[1], Is.Not.SameAs(attempts[0]));
        Assert.That(Context, Is.SameAs(attempts[1]));
        Assert.That(amendmentCount, Is.EqualTo(2));
    }

    private static async Task YieldAsync() => await Task.Yield();

    private void SetContextId(string value) => Context.Id = value;

    private void AssertContextId(string expected) => Assert.That(Context.Id, Is.EqualTo(expected));

    private void RecordAttempt(List<MyTestContext> attempts)
    {
        AssertContextId("amended");
        attempts.Add(Context);
        SetContextId("changed in given");
    }

    private static void FailFirstAttempt(List<MyTestContext> attempts)
    {
        if (attempts.Count == 1)
        {
            throw new InvalidOperationException("Retry with a fresh context");
        }
    }
}
