using BDTest.Helpers.JsonConverters;
using Newtonsoft.Json;

namespace BDTest.Test;

[JsonConverter(typeof(StoryTextConverter))]
public record StoryText
{
    [JsonProperty]
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