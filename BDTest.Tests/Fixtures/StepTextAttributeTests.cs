using BDTest;

namespace BDTest.Tests.Fixtures;

public class StepTextAttributeTests
{
    [Test]
    [Arguments("value=foo")]
    [Arguments("value={literal}:bar=baz")]
    [Arguments("value=")]
    [Arguments("0:legacy")]
    [Arguments("invalid")]
    [Arguments("")]
    [Arguments(null)]
    public async Task OverridesAreStoredWithoutThrowing(string? parameterOverride)
    {
        var attribute = new StepTextAttribute("Step {value}", parameterOverride!);
        await Assert.That(attribute.Text).IsEqualTo("Step {value}");
        await Assert.That(attribute.ParameterOverrides).IsEquivalentTo(new[] { parameterOverride });
    }

    [Test]
    public async Task OverridesAreSnapshotted()
    {
        var overrides = new[] { "value=original" };
        var attribute = new StepTextAttribute("Step {value}", overrides);
        overrides[0] = "value=changed";
        await Assert.That(attribute.ParameterOverrides.Single()).IsEqualTo("value=original");
    }

    [Test]
    public async Task NullAndEmptyArraysAreSafe()
    {
        await Assert.That(new StepTextAttribute("Step", (string[])null!).ParameterOverrides).IsEmpty();
        await Assert.That(new StepTextAttribute("Step", Array.Empty<string>()).ParameterOverrides).IsEmpty();
    }
}
