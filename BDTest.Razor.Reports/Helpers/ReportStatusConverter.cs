using System.Text.Json;
using System.Text.Json.Serialization;
using BDTest.Helpers;
using BDTest.Test;

namespace BDTest.Razor.Reports.Helpers;

// Reuse the core wire contract through its public metadata, without friend access.
internal sealed class ReportStatusConverter : JsonConverter<Status>
{
    private static readonly JsonConverter<Status> Converter =
        (JsonConverter<Status>)BDTestJsonContext.Default.Status.Converter;

    public override Status Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => Converter.Read(ref reader, typeToConvert, options);

    public override void Write(Utf8JsonWriter writer, Status value, JsonSerializerOptions options)
        => Converter.Write(writer, value, options);
}
