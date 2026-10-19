using BDTest.Attributes;
using BDTest.Exceptions;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class StoryAttributeTests
{
    [Test]
    [Arguments("a user", false, "As a user")]
    [Arguments("A tester", false, "As A tester")]
    [Arguments("an engineer", false, "As an engineer")]
    [Arguments("AN engineer", false, "As AN engineer")]
    [Arguments("engineer", false, "As an engineer")]
    [Arguments("admin", false, "As an admin")]
    [Arguments("Engineer", false, "As an Engineer")]
    [Arguments("inventor", false, "As an inventor")]
    [Arguments("owl", false, "As an owl")]
    [Arguments("umbrella maker", false, "As an umbrella maker")]
    [Arguments("user", false, "As a user")]
    [Arguments("User", false, "As a User")]
    [Arguments("username administrator", false, "As a username administrator")]
    [Arguments("owl", true, "As an owl")]
    [Arguments("user", true, "As an user")]
    [Arguments("a user", true, "As a user")]
    [Arguments("As a\tuser", false, "As a\tuser")]
    [Arguments("As a user", false, "As a user")]
    [Arguments("As an engineer", false, "As an engineer")]
    [Arguments("aS An engineer", true, "aS An engineer")]
    [Arguments("As a", false, "As a")]
    [Arguments("As an", true, "As an")]
    [Arguments("As another user", false, "As an As another user")]
    [Arguments("As aardvark keeper", false, "As an As aardvark keeper")]
    [Arguments("As another user", true, "As an As another user")]
    public async Task FormatsRoleWithoutChangingAttribute(string role, bool useAsAn, string expectedRole)
    {
        var attribute = new StoryAttribute
        {
            AsA = useAsAn ? null : role,
            AsAn = useAsAn ? role : null,
            IWant = "to test stories",
            SoThat = "reports are readable"
        };
        var expected = $"{expectedRole}{Environment.NewLine}I want to test stories{Environment.NewLine}So that reports are readable{Environment.NewLine}";

        await Assert.That(attribute.GetStoryText()).IsEqualTo(expected);
        await Assert.That(attribute.GetStoryText()).IsEqualTo(expected);
        using (Assert.Multiple())
        {
            await Assert.That(attribute.AsA).IsEqualTo(useAsAn ? null : role);
            await Assert.That(attribute.AsAn).IsEqualTo(useAsAn ? role : null);
            await Assert.That(attribute.IWant).IsEqualTo("to test stories");
            await Assert.That(attribute.SoThat).IsEqualTo("reports are readable");
        }
    }

    [Test]
    [Arguments("I want readable reports", "So that readers understand", "I want readable reports", "So that readers understand")]
    [Arguments("i WANT readable reports", "sO THAT readers understand", "i WANT readable reports", "sO THAT readers understand")]
    [Arguments("I want", "So that", "I want", "So that")]
    [Arguments("I want\treadable reports", "So that\treaders understand", "I want\treadable reports", "So that\treaders understand")]
    [Arguments("I want: readable reports", "So that: readers understand", "I want: readable reports", "So that: readers understand")]
    [Arguments("I want_more", "So that_more", "I want I want_more", "So that So that_more")]
    [Arguments("I wanted readable reports", "So thatch is readable", "I want I wanted readable reports", "So that So thatch is readable")]
    public async Task MatchesOnlyWholeWordPrefixes(string iWant, string soThat, string expectedWant, string expectedSoThat)
    {
        var attribute = new StoryAttribute { AsA = "user", IWant = iWant, SoThat = soThat };
        var expected = $"As a user{Environment.NewLine}{expectedWant}{Environment.NewLine}{expectedSoThat}{Environment.NewLine}";

        await Assert.That(attribute.GetStoryText()).IsEqualTo(expected);
        await Assert.That(attribute.GetStoryText()).IsEqualTo(expected);
        using (Assert.Multiple())
        {
            await Assert.That(attribute.IWant).IsEqualTo(iWant);
            await Assert.That(attribute.SoThat).IsEqualTo(soThat);
        }
    }

    [Test]
    [Arguments(null, null, "goal", "benefit", nameof(StoryAttribute.AsA))]
    [Arguments("", "", "goal", "benefit", nameof(StoryAttribute.AsA))]
    [Arguments("user", null, null, "benefit", nameof(StoryAttribute.IWant))]
    [Arguments(null, "owl", "", "benefit", nameof(StoryAttribute.IWant))]
    [Arguments("user", null, "goal", null, nameof(StoryAttribute.SoThat))]
    [Arguments(null, "owl", "goal", "", nameof(StoryAttribute.SoThat))]
    public async Task RejectsMissingArguments(string? asA, string? asAn, string? iWant, string? soThat, string missingArgument)
    {
        var attribute = new StoryAttribute { AsA = asA, AsAn = asAn, IWant = iWant, SoThat = soThat };

        var exception = await Assert.That(() => attribute.GetStoryText()).ThrowsExactly<MissingStoryTextArgumentException>();

        await Assert.That(Required(exception).Message).IsEqualTo(new MissingStoryTextArgumentException(missingArgument).Message);
    }

    [Test]
    public async Task RejectsDuplicateRolesBeforeMissingGoal()
    {
        var attribute = new StoryAttribute { AsA = "user", AsAn = "owl" };

        await Assert.That(() => attribute.GetStoryText()).ThrowsExactly<DuplicateStoryTextArgumentException>();
    }
}
