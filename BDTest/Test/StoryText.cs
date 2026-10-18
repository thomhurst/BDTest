using BDTest.Helpers.JsonConverters;
using System.Text.Json.Serialization;

namespace BDTest.Test;

[JsonConverter(typeof(StoryTextConverter))]
public record StoryText
{
    public string Story { get; private set; }

    public StoryText(string story)
    {
        Story = story;
    }

    [JsonConstructor]
    private StoryText()
    {
    }
}