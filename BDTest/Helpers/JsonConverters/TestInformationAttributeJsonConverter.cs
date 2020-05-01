using System;
using BDTest.Attributes;
using Newtonsoft.Json;

namespace BDTest.Helpers.JsonConverters
{
    public class TestInformationAttributeJsonConverter : JsonConverter<TestInformationAttribute>
    {
        public override void WriteJson(JsonWriter writer, TestInformationAttribute value, JsonSerializer serializer)
        {
            if (value == null)
            {
                return;
            }
            
            writer.WriteValue(value.Print());
        }

        public override TestInformationAttribute ReadJson(JsonReader reader, Type objectType, TestInformationAttribute existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}