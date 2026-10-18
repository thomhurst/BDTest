using System.Collections.Concurrent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = 4, Scope = ExecutionScope.MethodLevel)]

namespace BDTest.MSTest.Tests;

[BDTestClass]
public class ExecutionIdTests : MSTestBDTestBase<ExecutionIdTests.TestContextData>
{
    private static readonly ConcurrentDictionary<string, byte> ExecutionIds = new();
    // Assigned by TestInitialize before each test body.
    private TestContextData _setupContext = null!;
    private string _setupId = null!;

    [TestInitialize]
    public async Task AsyncSetup()
    {
        Console.WriteLine("sync console startup " + BDTestExecutionId);
        await Task.Yield();
        await Task.Run(() =>
        {
            Console.WriteLine("async console startup " + BDTestExecutionId);
            _setupContext = Context;
            _setupId = BDTestContext.CurrentScenarioBDTestExecutionId;
            WriteStartupOutput("startup " + _setupId);
        });
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public async Task SetupAndBodyShareContextAndOutput(int row)
    {
        Console.WriteLine("body console startup " + _setupId);
        Assert.AreSame(_setupContext, Context);
        Assert.AreEqual(_setupId, BDTestContext.CurrentScenarioBDTestExecutionId);
        Assert.IsTrue(ExecutionIds.TryAdd(_setupId, 0), $"Row {row} reused another test's ID");

        var scenario = await When(() => NoOp()).Then(() => NoOp()).BDTestAsync();

        Assert.AreEqual(_setupId, scenario.FrameworkTestId);
        var startupOutput = scenario.TestStartupInformation;
        Assert.IsNotNull(startupOutput);
        Assert.Contains("startup " + _setupId, startupOutput);
        Assert.Contains("sync console startup " + _setupId, startupOutput);
        Assert.Contains("async console startup " + _setupId, startupOutput);
        Assert.Contains("body console startup " + _setupId, startupOutput);
        var testContext = TestContext;
        Assert.IsNotNull(testContext);
        Assert.Contains(testContext.FullyQualifiedTestClassName, _setupId);
        Assert.IsNotNull(testContext.TestDisplayName);
        Assert.Contains(testContext.TestDisplayName, _setupId);
        WriteTearDownOutput("teardown " + _setupId);
        var teardownOutput = scenario.TearDownOutput;
        Assert.IsNotNull(teardownOutput);
        Assert.Contains("teardown " + _setupId, teardownOutput);
    }

    private static void NoOp() { }

    public sealed class TestContextData { }
}
