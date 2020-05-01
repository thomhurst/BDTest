using System;
using BDTest.Test;
using Newtonsoft.Json;

namespace BDTest.Helpers.JsonConverters
{
    public class StoryTextConverter : JsonConverter<StoryText>
    {
        public override void WriteJson(JsonWriter writer, StoryText value, JsonSerializer serializer)
        {
            if (value == null)
            {
                return;
            }
            
            writer.WriteValue(value.Story);
        }

        public override StoryText ReadJson(JsonReader reader, Type objectType, StoryText existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            return new StoryText(reader.Value?.ToString());
        }
    }
}