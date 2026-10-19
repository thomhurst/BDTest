using System.Text.Json.Serialization;
using BDTest.Attributes;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Test;

namespace BDTest.Helpers;

/// <summary>Generated metadata for the report wire format, independent of host JSON options.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, Converters = new[] { typeof(StatusConverter) })]
[JsonSerializable(typeof(BDTestOutputModel))]
[JsonSerializable(typeof(List<Scenario>))]
[JsonSerializable(typeof(StoryText))]
[JsonSerializable(typeof(ScenarioText))]
[JsonSerializable(typeof(TestInformationAttribute))]
[JsonSerializable(typeof(TimeSpan))]
[JsonSerializable(typeof(StepArgument))]
[JsonSerializable(typeof(StepTable))]
[JsonSerializable(typeof(StepDocString))]
[JsonSerializable(typeof(Attachment))]
[JsonSerializable(typeof(ExceptionWrapper))]
[System.Diagnostics.CodeAnalysis.SuppressMessage("ApiDesign", "RS0041", Justification = "System.Text.Json generates nullable-oblivious JsonTypeInfo properties; their signatures remain checked by the public API baseline.")]
public partial class BDTestJsonContext : JsonSerializerContext
{
}
