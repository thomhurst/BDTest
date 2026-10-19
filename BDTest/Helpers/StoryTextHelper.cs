using BDTest.Test;

namespace BDTest.Helpers;

internal static class StoryTextHelper
{
    public static StoryText? GetStoryText(BDTestBase bdTestBase)
    {
        var storyText = bdTestBase.GetStoryText();
        return storyText == null ? null : new StoryText(storyText);
    }
}
