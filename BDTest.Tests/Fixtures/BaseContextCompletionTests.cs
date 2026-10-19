using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class BaseContextCompletionTests
{
    [Test]
    public async Task CompletionRemovesContextBeforeResettingExecutionId()
    {
        using var fixture = new TestFixture();
        var executionId = fixture.ExecutionId;
        var original = fixture.Context;

        fixture.Complete();

        await Assert.That(fixture.ExecutionId).IsNotEqualTo(executionId);
        fixture.SelectedExecutionId = executionId;
        await Assert.That(fixture.Context).IsNotSameReferenceAs(original);
        fixture.Complete();
    }

    [Test]
    public async Task CompletionPreservesOtherExecutionContexts()
    {
        using var fixture = new TestFixture { SelectedExecutionId = "first" };
        var first = fixture.Context;
        fixture.SelectedExecutionId = "second";
        var second = fixture.Context;

        fixture.Complete();

        fixture.SelectedExecutionId = "first";
        await Assert.That(fixture.Context).IsSameReferenceAs(first);
        fixture.SelectedExecutionId = "second";
        await Assert.That(fixture.Context).IsNotSameReferenceAs(second);
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
