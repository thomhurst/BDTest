using System.Collections.Concurrent;
using BDTest.Test;
using Xunit;
using Xunit.v3;

// Each concrete class has its own default collection. Allow all participants to start even on single-core runners.
[assembly: Parallelization(MaxThreads = BDTest.xUnit.Tests.XUnitParallelTestsUniqueId.ParticipantCount)]

namespace BDTest.xUnit.Tests;

public abstract class XUnitParallelTestsUniqueId : XUnitBDTestBase<XUnitContext>
{
    public const int ParticipantCount = 5;

    private static readonly ConcurrentDictionary<string, XUnitContext> Contexts = new();
    private static readonly TaskCompletionSource AllTestsStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task ExecutionIdAndContextRemainIsolatedDuringConcurrentScenarios()
    {
        var id = BDTestContext.CurrentScenarioBDTestExecutionId;
        var context = Context;

        var scenario = await When(() => WaitForOtherTests(id, context))
            .Then(() => AssertIsolatedContext(id, context))
            .BDTestAsync(global::Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(id, scenario.FrameworkTestId);
        Assert.Equal(Status.Passed, scenario.Status);
    }

    private static async Task WaitForOtherTests(string id, XUnitContext context)
    {
        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.True(Contexts.TryAdd(id, context), $"Duplicate execution ID: {id}");

        if (Contexts.Count == ParticipantCount)
        {
            AllTestsStarted.TrySetResult();
        }

        // No scenario can finish until all five collections are executing a step.
        // Run this group together when filtering: FullyQualifiedName~XUnitParallelTestsUniqueId.
        await AllTestsStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await Task.Yield();
    }

    private void AssertIsolatedContext(string id, XUnitContext context)
    {
        Assert.Equal(ParticipantCount, Contexts.Count);
        Assert.Equal(ParticipantCount, Contexts.Values.Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.Equal(id, BDTestContext.CurrentScenarioBDTestExecutionId);
        Assert.Same(context, Context);
    }
}

public class XUnitParallelTestsUniqueId1 : XUnitParallelTestsUniqueId;
public class XUnitParallelTestsUniqueId2 : XUnitParallelTestsUniqueId;
public class XUnitParallelTestsUniqueId3 : XUnitParallelTestsUniqueId;
public class XUnitParallelTestsUniqueId4 : XUnitParallelTestsUniqueId;
public class XUnitParallelTestsUniqueId5 : XUnitParallelTestsUniqueId;
