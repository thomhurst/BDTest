using System.Collections.Concurrent;
using Xunit;
using Xunit.Abstractions;
using BDTest.TestUtilities;

namespace BDTest.xUnit.Tests;

public class LifecycleTests : XUnitBDTestBase<XUnitContext>
{
    private static readonly ConcurrentDictionary<string, byte> Ids = new();
    private readonly ITestOutputHelper _output;

    public LifecycleTests(ITestOutputHelper output) : base(output)
    {
        _output = output;
        Console.WriteLine("startup:" + BDTestExecutionId);
    }

    // Non-serializable data forces v2 to execute rows from one shared test case.
    public static IEnumerable<object[]> Rows => new[] { new object[] { new object() }, new object[] { new object() } };

    [Theory]
    [MemberData(nameof(Rows))]
    public async Task NonSerializableRowsHaveUniqueIdsAndCaptureConstructorOutput(object row)
    {
        var id = BDTestExecutionId;
        var context = Context;
        Assert.True(Ids.TryAdd(id, 0), $"Duplicate execution ID: {id}");
        await Task.Yield();
        Assert.Equal(id, BDTestExecutionId);
        Assert.Same(context, Context);
        var scenario = await When(() => GC.KeepAlive(row)).Then(() => NoOp()).BDTestAsync();
        Assert.Equal(id, scenario.FrameworkTestId);
        Assert.Contains("startup:" + id, scenario.TestStartupInformation);
    }

    private static void NoOp() { }

    [Fact]
    public Task ExplicitRunsKeepCompletionAndOutputIsolated() =>
        LifecycleContract.Verify(run => new Probe(run, _output).Actions);

    private sealed class Probe(BDTestRun run, ITestOutputHelper output) : XUnitBDTestBase<XUnitContext>(output)
    {
        public override BDTestRun TestRun => run;
        public LifecycleContract.Probe Actions => new(this, BeginTest, EndTest, () => Context);
    }
}
