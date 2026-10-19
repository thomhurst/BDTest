using BDTest;
using BDTest.TestUtilities;
using Xunit;

namespace BDTest.xUnit.Tests;

[BDTestTag("fixture")]
public class MethodIdentityTests : XUnitBDTestBase<object>
{
#pragma warning disable xUnit1031 // Exercise synchronous test method identity explicitly.
    [Fact]
    [ScenarioText("Synchronous metadata"), TestInformation("Synchronous metadata"), BDTestTag("Synchronous metadata"), BDTestRetry(1)]
    public void Synchronous() =>
        MethodIdentityContract.Verify(this, "Synchronous metadata", false).GetAwaiter().GetResult();
#pragma warning restore xUnit1031

    [Fact]
    [ScenarioText("Async metadata"), TestInformation("Async metadata"), BDTestTag("Async metadata"), BDTestRetry(1)]
    public Task AfterAwait() =>
        MethodIdentityContract.Verify(this, "Async metadata", true);

    [Theory, InlineData(1)]
    [ScenarioText("Integer metadata"), TestInformation("Integer metadata"), BDTestTag("Integer metadata"), BDTestRetry(1)]
    public Task IntegerRow(int value)
    {
        Assert.Equal(1, value);
        return MethodIdentityContract.Verify(this, "Integer metadata", true);
    }

    [Theory, InlineData("value")]
    [ScenarioText("String metadata"), TestInformation("String metadata"), BDTestTag("String metadata"), BDTestRetry(1)]
    public Task StringRow(string value)
    {
        Assert.Equal("value", value);
        return MethodIdentityContract.Verify(this, "String metadata", true);
    }

    [Fact]
    [ScenarioText("Private metadata"), TestInformation("Private metadata"), BDTestTag("Private metadata"), BDTestRetry(1)]
    private Task NonPublicTest() => MethodIdentityContract.Verify(this, "Private metadata", true);
}
