using BDTest.Attributes;
using BDTest.Exceptions;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class StepTextAttributeTests
{
    [Test]
    [Arguments("0:foo")]
    [Arguments("12:foo:bar")]
    [Arguments("0:")]
    [Arguments("")]
    [Arguments(null)]
    public async Task SingleOverride_AcceptsValidOrEmptyValues(string? parameterOverride)
    {
        var attribute = new StepTextAttribute("Step {0}", parameterOverride!);

        await Assert.That(attribute.Text).IsEqualTo("Step {0}");
        await Assert.That(attribute.StepTextParameterOverrides).IsEquivalentTo(new[] { parameterOverride }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("foo")]
    [Arguments(":foo")]
    [Arguments("0foo")]
    [Arguments("-1:foo")]
    [Arguments("foo0:bar")]
    public async Task SingleOverride_RejectsMissingIndexOrColon(string parameterOverride)
    {
        await Assert.That(() => new StepTextAttribute("Step {0}", parameterOverride)).ThrowsExactly<InvalidStepTextParameterException>();
    }

    [Test]
    [Arguments("0:foo")]
    [Arguments("12:foo:bar")]
    [Arguments("0:")]
    [Arguments("")]
    [Arguments(null)]
    public async Task ArrayOverrides_AcceptsValidOrEmptyValues(string? parameterOverride)
    {
        var overrides = new[] { "1:bar", parameterOverride! };

        var attribute = new StepTextAttribute("Step {0} {1}", overrides);

        await Assert.That(attribute.Text).IsEqualTo("Step {0} {1}");
        await Assert.That(attribute.StepTextParameterOverrides).IsEquivalentTo(overrides, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("foo")]
    [Arguments(":foo")]
    [Arguments("0foo")]
    [Arguments("-1:foo")]
    [Arguments("foo0:bar")]
    public async Task ArrayOverrides_RejectsMissingIndexOrColon(string parameterOverride)
    {
        await Assert.That(() =>
            new StepTextAttribute("Step {0}", new[] { parameterOverride })).ThrowsExactly<InvalidStepTextParameterException>();
    }

    [Test]
    public async Task ArrayOverrides_RejectsInvalidValueAfterValidValue()
    {
        await Assert.That(() =>
            new StepTextAttribute("Step {0} {1}", new[] { "0:foo", "invalid" })).ThrowsExactly<InvalidStepTextParameterException>();
    }

    [Test]
    public async Task ArrayOverrides_AcceptsEmptyArray()
    {
        var attribute = new StepTextAttribute("Step", Array.Empty<string>());

        await Assert.That(attribute.StepTextParameterOverrides).IsEmpty();
    }
}
