using BDTest.Attributes;
using BDTest.Exceptions;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class StoryAttributeTests
{
    [TestCase("a user", false, "As a user")]
    [TestCase("A tester", false, "As A tester")]
    [TestCase("an engineer", false, "As an engineer")]
    [TestCase("AN engineer", false, "As AN engineer")]
    [TestCase("engineer", false, "As an engineer")]
    [TestCase("admin", false, "As an admin")]
    [TestCase("Engineer", false, "As an Engineer")]
    [TestCase("inventor", false, "As an inventor")]
    [TestCase("owl", false, "As an owl")]
    [TestCase("umbrella maker", false, "As an umbrella maker")]
    [TestCase("user", false, "As a user")]
    [TestCase("User", false, "As a User")]
    [TestCase("username administrator", false, "As a username administrator")]
    [TestCase("owl", true, "As an owl")]
    [TestCase("user", true, "As an user")]
    [TestCase("a user", true, "As a user")]
    [TestCase("As a\tuser", false, "As a\tuser")]
    [TestCase("As a user", false, "As a user")]
    [TestCase("As an engineer", false, "As an engineer")]
    [TestCase("aS An engineer", true, "aS An engineer")]
    [TestCase("As a", false, "As a")]
    [TestCase("As an", true, "As an")]
    [TestCase("As another user", false, "As an As another user")]
    [TestCase("As aardvark keeper", false, "As an As aardvark keeper")]
    [TestCase("As another user", true, "As an As another user")]
    public void FormatsRoleWithoutChangingAttribute(string role, bool useAsAn, string expectedRole)
    {
        var attribute = new StoryAttribute
        {
            AsA = useAsAn ? null : role,
            AsAn = useAsAn ? role : null,
            IWant = "to test stories",
            SoThat = "reports are readable"
        };
        var expected = $"{expectedRole}{Environment.NewLine}I want to test stories{Environment.NewLine}So that reports are readable{Environment.NewLine}";

        Assert.That(attribute.GetStoryText(), Is.EqualTo(expected));
        Assert.That(attribute.GetStoryText(), Is.EqualTo(expected));
        Assert.Multiple(() =>
        {
            Assert.That(attribute.AsA, Is.EqualTo(useAsAn ? null : role));
            Assert.That(attribute.AsAn, Is.EqualTo(useAsAn ? role : null));
            Assert.That(attribute.IWant, Is.EqualTo("to test stories"));
            Assert.That(attribute.SoThat, Is.EqualTo("reports are readable"));
        });
    }

    [TestCase("I want readable reports", "So that readers understand", "I want readable reports", "So that readers understand")]
    [TestCase("i WANT readable reports", "sO THAT readers understand", "i WANT readable reports", "sO THAT readers understand")]
    [TestCase("I want", "So that", "I want", "So that")]
    [TestCase("I want\treadable reports", "So that\treaders understand", "I want\treadable reports", "So that\treaders understand")]
    [TestCase("I want: readable reports", "So that: readers understand", "I want: readable reports", "So that: readers understand")]
    [TestCase("I want_more", "So that_more", "I want I want_more", "So that So that_more")]
    [TestCase("I wanted readable reports", "So thatch is readable", "I want I wanted readable reports", "So that So thatch is readable")]
    public void MatchesOnlyWholeWordPrefixes(string iWant, string soThat, string expectedWant, string expectedSoThat)
    {
        var attribute = new StoryAttribute { AsA = "user", IWant = iWant, SoThat = soThat };
        var expected = $"As a user{Environment.NewLine}{expectedWant}{Environment.NewLine}{expectedSoThat}{Environment.NewLine}";

        Assert.That(attribute.GetStoryText(), Is.EqualTo(expected));
        Assert.That(attribute.GetStoryText(), Is.EqualTo(expected));
        Assert.Multiple(() =>
        {
            Assert.That(attribute.IWant, Is.EqualTo(iWant));
            Assert.That(attribute.SoThat, Is.EqualTo(soThat));
        });
    }

    [TestCase(null, null, "goal", "benefit", nameof(StoryAttribute.AsA))]
    [TestCase("", "", "goal", "benefit", nameof(StoryAttribute.AsA))]
    [TestCase("user", null, null, "benefit", nameof(StoryAttribute.IWant))]
    [TestCase(null, "owl", "", "benefit", nameof(StoryAttribute.IWant))]
    [TestCase("user", null, "goal", null, nameof(StoryAttribute.SoThat))]
    [TestCase(null, "owl", "goal", "", nameof(StoryAttribute.SoThat))]
    public void RejectsMissingArguments(string asA, string asAn, string iWant, string soThat, string missingArgument)
    {
        var attribute = new StoryAttribute { AsA = asA, AsAn = asAn, IWant = iWant, SoThat = soThat };

        var exception = Assert.Throws<MissingStoryTextArgumentException>(() => attribute.GetStoryText());

        Assert.That(Required(exception).Message, Is.EqualTo(new MissingStoryTextArgumentException(missingArgument).Message));
    }

    [Test]
    public void RejectsDuplicateRolesBeforeMissingGoal()
    {
        var attribute = new StoryAttribute { AsA = "user", AsAn = "owl" };

        Assert.Throws<DuplicateStoryTextArgumentException>(() => attribute.GetStoryText());
    }
}
