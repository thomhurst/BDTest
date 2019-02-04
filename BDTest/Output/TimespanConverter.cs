using System;
using Newtonsoft.Json;

namespace BDTest.Output
{
    public class TimespanConverter : JsonConverter<TimeSpan>
    {
        /// <summary>
        /// Format: Days.Hours:Minutes:Seconds:Milliseconds
        /// </summary>
        public static string TimeSpanFormatString { get; } = @"d\.hh\:mm\:ss\:fffffff";

        public override void WriteJson(JsonWriter writer, TimeSpan value, JsonSerializer serializer)
        {
            var timespanFormatted = $"{value.ToString(TimeSpanFormatString)}";
            writer.WriteValue(timespanFormatted);
        }

        public override TimeSpan ReadJson(JsonReader reader, Type objectType, TimeSpan existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            TimeSpan.TryParseExact((string)reader.Value, TimeSpanFormatString, null, out var parsedTimeSpan);
            return parsedTimeSpan;
        }
    }
}
