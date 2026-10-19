using BDTest;
using BDTest.Test;

namespace BDTest.Tests.Fixtures;

[Story(AsA = "BDTest developer",
    IWant = "to make sure that test contexts are different for each test",
    SoThat = "tests don't share state")]
public class UniqueContextTests
{
    public class AsyncNonNUnitContextTests
    {
        [Test]
        public Task TestWithContext1() => AssertSequentialContextsAsync();

        [Test]
        public Task TestWithContext2() => AssertSequentialContextsAsync();

        private static async Task AssertSequentialContextsAsync()
        {
            using var fixture = new StandaloneContext();
            var first = await Task.Run(() => CaptureAndComplete(fixture));
            var second = await Task.Run(() => CaptureAndComplete(fixture));
            await Assert.That(first.Id).IsNotEqualTo(second.Id);
        }
    }

    public class NonNUnitContextTests
    {
        [Test]
        public Task TestWithContext1() => AssertSequentialContexts();

        [Test]
        public Task TestWithContext2() => AssertSequentialContexts();

        private static async Task AssertSequentialContexts()
        {
            using var fixture = new StandaloneContext();
            var first = CaptureAndComplete(fixture);
            var second = CaptureAndComplete(fixture);
            await Assert.That(first.Id).IsNotEqualTo(second.Id);
        }
    }

    public class NonNUnitParallelContextTests
    {
        // Preserve the eight capture cases, but give each case its own shared harness.
        // Filtered tests no longer need other runner cases to execute first.
        [Test]
        [Arguments(0)]
        [Arguments(1)]
        [Arguments(2)]
        [Arguments(3)]
        [Arguments(4)]
        [Arguments(5)]
        [Arguments(6)]
        [Arguments(7)]
        public async Task ContextIsIndependentOfOtherFlows(int index)
        {
            var contexts = await CaptureParallelContexts();
            await Assert.That(contexts.Where((_, position) => position != index)
                .All(context => context.Id != contexts[index].Id)).IsTrue();
        }

        [Test]
        public async Task EnsureContextsAreUnique()
        {
            var contexts = await CaptureParallelContexts();
            await Assert.That(contexts.Select(context => context.Id).Distinct().Count()).IsEqualTo(8);
        }

        private static async Task<MyTestContext[]> CaptureParallelContexts()
        {
            using var fixture = new StandaloneContext();
            Task<MyTestContext>[] tasks;
            // Model independent standalone callers without carrying the runner's ambient identity.
            using (ExecutionContext.SuppressFlow())
            {
                tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(async () =>
                {
                    try
                    {
                        var context = fixture.Context;
                        await Task.Yield();
                        await Assert.That(fixture.Context).IsSameReferenceAs(context);
                        return context;
                    }
                    finally
                    {
                        LegacyLifecycle.Complete(fixture);
                    }
                })).ToArray();
            }
            return await Task.WhenAll(tasks);
        }
    }

    private static MyTestContext CaptureAndComplete(StandaloneContext fixture)
    {
        try
        {
            return fixture.Context;
        }
        finally
        {
            // Exercise completion without a preceding BeginTest.
            LegacyLifecycle.Complete(fixture);
        }
    }

    private sealed class StandaloneContext : RunContextTestBase<MyTestContext>;
}
