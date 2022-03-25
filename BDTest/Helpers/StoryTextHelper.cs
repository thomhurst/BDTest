using System.Reflection;
using BDTest.Attributes;
using BDTest.Test;

namespace BDTest.Helpers;

public static class StoryTextHelper
{
    public static StoryText GetStoryText(BDTestBase bdTestBase)
    {
        var classStoryAttribute =
            FindStoryAttribute(bdTestBase);

        if (classStoryAttribute == null)
        {
            return null;
        }

        return new StoryText(classStoryAttribute.GetStoryText());
    }

    private static StoryAttribute FindStoryAttribute(BDTestBase bdTestBase)
    {
        return bdTestBase.GetType().GetCustomAttribute(typeof(StoryAttribute)) as StoryAttribute;
    }
}