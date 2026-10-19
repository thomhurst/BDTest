using BDTest;
using BDTest.TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.CompilerServices;

namespace BDTest.MSTest.Tests;

[BDTestClass]
[BDTestTag("fixture")]
public class MethodIdentityTests : MSTestBDTestBase<object>
{
    private static readonly AsyncLocal<bool> CustomExecution = new();

    [CustomTestMethod]
    [ScenarioText("Synchronous metadata"), TestInformation("Synchronous metadata"), BDTestTag("Synchronous metadata"), BDTestRetry(1)]
    public void Synchronous()
    {
        Assert.IsTrue(CustomExecution.Value, "The original test method attribute must execute.");
        MethodIdentityContract.Verify(this, "Synchronous metadata", false).GetAwaiter().GetResult();
    }

    [TestMethod]
    [ScenarioText("Async metadata"), TestInformation("Async metadata"), BDTestTag("Async metadata"), BDTestRetry(1)]
    public Task AfterAwait() =>
        MethodIdentityContract.Verify(this, "Async metadata", true);

    [TestMethod, DataRow(1)]
    [ScenarioText("Integer metadata"), TestInformation("Integer metadata"), BDTestTag("Integer metadata"), BDTestRetry(1)]
    public Task Overloaded(int value) =>
        MethodIdentityContract.Verify(this, "Integer metadata", true);

    [TestMethod, DataRow("value")]
    [ScenarioText("String metadata"), TestInformation("String metadata"), BDTestTag("String metadata"), BDTestRetry(1)]
    public Task Overloaded(string value) =>
        MethodIdentityContract.Verify(this, "String metadata", true);

    public sealed class CustomTestMethodAttribute : TestMethodAttribute
    {
        public CustomTestMethodAttribute([CallerFilePath] string file = "", [CallerLineNumber] int line = -1)
            : base(file, line) { }

        public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
        {
            CustomExecution.Value = true;
            try { return await base.ExecuteAsync(testMethod); }
            finally { CustomExecution.Value = false; }
        }
    }
}
