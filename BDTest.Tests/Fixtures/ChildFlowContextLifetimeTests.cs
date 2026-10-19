using System.Runtime.CompilerServices;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class ChildFlowContextLifetimeTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CompletedChildFlowDoesNotRetainContext(bool overwrite)
    {
        using var fixture = new TestFixture();
        fixture.Complete(); // Ensure the child creates its own execution ID.
        var context = CreateChildContext(fixture, overwrite);
        fixture.Complete();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Assert.That(context.IsAlive).IsFalse();
        GC.KeepAlive(fixture);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateChildContext(TestFixture fixture, bool overwrite)
    {
        return Task.Run(() =>
        {
            if (overwrite)
            {
                fixture.OverwriteContext(new MyTestContext());
            }

            var original = fixture.Context;
            GC.Collect();
            RequireCondition(ReferenceEquals(fixture.Context, original), "An active flow must retain its context");
            return new WeakReference(original);
        }).GetAwaiter().GetResult();
    }

    private sealed class TestFixture : RunContextTestBase<MyTestContext>
    {
        // Exercise completion of standalone contexts that never called BeginTest.
        public void Complete() => BDTest.Tests.Helpers.LegacyLifecycle.Complete(this);
    }
}
