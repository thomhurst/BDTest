using BDTest;
using BDTest.TestUtilities;
using NUnit.Framework;

namespace BDTest.Tests;

[TestFixture]
[BDTestTag("fixture")]
public class MethodIdentityTests : NUnitBDTestBase<object>
{
    [Test]
    [ScenarioText("Synchronous metadata"), TestInformation("Synchronous metadata"), BDTestTag("Synchronous metadata"), BDTestRetry(1)]
    public void Synchronous() =>
        MethodIdentityContract.Verify(this, "Synchronous metadata", false).GetAwaiter().GetResult();

    [Test]
    [ScenarioText("Async metadata"), TestInformation("Async metadata"), BDTestTag("Async metadata"), BDTestRetry(1)]
    public Task AfterAwait() =>
        MethodIdentityContract.Verify(this, "Async metadata", true);

    [TestCase(1)]
    [ScenarioText("Integer metadata"), TestInformation("Integer metadata"), BDTestTag("Integer metadata"), BDTestRetry(1)]
    public Task Overloaded(int value) =>
        MethodIdentityContract.Verify(this, "Integer metadata", true);

    [TestCase("value")]
    [ScenarioText("String metadata"), TestInformation("String metadata"), BDTestTag("String metadata"), BDTestRetry(1)]
    public Task Overloaded(string value) =>
        MethodIdentityContract.Verify(this, "String metadata", true);
}
