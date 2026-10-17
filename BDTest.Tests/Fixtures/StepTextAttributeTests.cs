using BDTest.Attributes;
using BDTest.Exceptions;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class StepTextAttributeTests
{
    [TestCase("0:foo")]
    [TestCase("12:foo:bar")]
    [TestCase("0:")]
    [TestCase("")]
    [TestCase(null)]
    public void SingleOverride_AcceptsValidOrEmptyValues(string parameterOverride)
    {
        var attribute = new StepTextAttribute("Step {0}", parameterOverride);

        Assert.That(attribute.Text, Is.EqualTo("Step {0}"));
        Assert.That(attribute.StepTextParameterOverrides, Is.EqualTo(new[] { parameterOverride }));
    }

    [TestCase("foo")]
    [TestCase(":foo")]
    [TestCase("0foo")]
    [TestCase("-1:foo")]
    [TestCase("foo0:bar")]
    public void SingleOverride_RejectsMissingIndexOrColon(string parameterOverride)
    {
        Assert.Throws<InvalidStepTextParameterException>(() => new StepTextAttribute("Step {0}", parameterOverride));
    }

    [TestCase("0:foo")]
    [TestCase("12:foo:bar")]
    [TestCase("0:")]
    [TestCase("")]
    [TestCase(null)]
    public void ArrayOverrides_AcceptsValidOrEmptyValues(string parameterOverride)
    {
        var overrides = new[] { "1:bar", parameterOverride };

        var attribute = new StepTextAttribute("Step {0} {1}", overrides);

        Assert.That(attribute.Text, Is.EqualTo("Step {0} {1}"));
        Assert.That(attribute.StepTextParameterOverrides, Is.EqualTo(overrides));
    }

    [TestCase("foo")]
    [TestCase(":foo")]
    [TestCase("0foo")]
    [TestCase("-1:foo")]
    [TestCase("foo0:bar")]
    public void ArrayOverrides_RejectsMissingIndexOrColon(string parameterOverride)
    {
        Assert.Throws<InvalidStepTextParameterException>(() =>
            new StepTextAttribute("Step {0}", new[] { parameterOverride }));
    }

    [Test]
    public void ArrayOverrides_RejectsInvalidValueAfterValidValue()
    {
        Assert.Throws<InvalidStepTextParameterException>(() =>
            new StepTextAttribute("Step {0} {1}", new[] { "0:foo", "invalid" }));
    }

    [Test]
    public void ArrayOverrides_AcceptsEmptyArray()
    {
        var attribute = new StepTextAttribute("Step", Array.Empty<string>());

        Assert.That(attribute.StepTextParameterOverrides, Is.Empty);
    }
}
