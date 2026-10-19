using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class BaseContextCompletionTests
{
    [Test]
    public void CompletionRemovesContextBeforeResettingExecutionId()
    {
        using var fixture = new TestFixture();
        var executionId = fixture.ExecutionId;
        var original = fixture.Context;

        fixture.Complete();

        Assert.That(fixture.ExecutionId, Is.Not.EqualTo(executionId));
        fixture.SelectedExecutionId = executionId;
        Assert.That(fixture.Context, Is.Not.SameAs(original));
        fixture.Complete();
    }

    [Test]
    public void CompletionPreservesOtherExecutionContexts()
    {
        using var fixture = new TestFixture { SelectedExecutionId = "first" };
        var first = fixture.Context;
        fixture.SelectedExecutionId = "second";
        var second = fixture.Context;

        fixture.Complete();

        fixture.SelectedExecutionId = "first";
        Assert.That(fixture.Context, Is.SameAs(first));
        fixture.SelectedExecutionId = "second";
        Assert.That(fixture.Context, Is.Not.SameAs(second));
        fixture.Complete();
    }

    private sealed class TestFixture : RunContextTestBase<MyTestContext>
    {
        public string? SelectedExecutionId { get; set; }
        public string ExecutionId => BDTestExecutionId;
        protected override string BDTestExecutionId => SelectedExecutionId ?? base.BDTestExecutionId;
        // Exercise completion of standalone contexts that never called BeginTest.
        public void Complete() => BDTest.Tests.Helpers.LegacyLifecycle.Complete(this);
    }
}
