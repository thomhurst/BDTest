using System.Collections.Concurrent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Parallelize(Workers = 4, Scope = ExecutionScope.MethodLevel)]

namespace BDTest.MSTest.Tests;

[BDTestClass]
public class ExecutionIdTests : MSTestBDTestBase<ExecutionIdTests.TestContextData>
{
    private static readonly ConcurrentDictionary<string, byte> ExecutionIds = new();
    private TestContextData _setupContext;
    private string _setupId;

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
        Assert.Contains("startup " + _setupId, scenario.TestStartupInformation);
        Assert.Contains("sync console startup " + _setupId, scenario.TestStartupInformation);
        Assert.Contains("async console startup " + _setupId, scenario.TestStartupInformation);
        Assert.Contains("body console startup " + _setupId, scenario.TestStartupInformation);
        Assert.Contains(TestContext.FullyQualifiedTestClassName, _setupId);
        Assert.Contains(TestContext.TestDisplayName, _setupId);
        WriteTearDownOutput("teardown " + _setupId);
        Assert.Contains("teardown " + _setupId, scenario.TearDownOutput);
    }

    private static void NoOp() { }

    public sealed class TestContextData { }
}
