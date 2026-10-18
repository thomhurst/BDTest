using System.Text.Json.Serialization;

namespace BDTest.Contracts;

/// <summary>The execution status, serialized by name in schema-versioned reports.</summary>
[JsonConverter(typeof(StatusJsonConverter))]
public enum Status
{
    NotRun,
    Pending,
    Passed,
    Failed,
    Inconclusive,
    NotImplemented,
    Skipped
}

internal sealed class StatusJsonConverter : JsonStringEnumConverter<Status>
{
    public StatusJsonConverter() : base(namingPolicy: null, allowIntegerValues: false)
    {
    }
}
