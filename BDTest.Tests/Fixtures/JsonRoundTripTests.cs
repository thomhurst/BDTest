using BDTest.Output;
using BDTest.Helpers;
using BDTest.Test;
using BDTest.Test.Steps;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class JsonRoundTripTests : TestRunOwner
{
    private static readonly BDTestJsonContext DurationContext = new(new JsonSerializerOptions
    {
        Converters = { new TimespanConverter() }
    });

    [Test]
    [Arguments(StepType.Given, "Given")]
    [Arguments(StepType.AndGiven, "And")]
    [Arguments(StepType.When, "When")]
    [Arguments(StepType.Then, "Then")]
    [Arguments(StepType.AndThen, "And")]
    [Arguments(StepType.AndWhen, "And")]
    [Arguments(StepType.ButGiven, "But")]
    [Arguments(StepType.ButWhen, "But")]
    [Arguments(StepType.ButThen, "But")]
    public async Task Step_round_trip_preserves_type_and_reporting_prefix(StepType stepType, string prefix)
    {
        var step = new Step(new Runnable(() => NoOp()), stepType, "scenario", "report", TestRun);
        var restored = ReportJson.DeserializeObject<Step>(ReportJson.SerializeObject(step));

        await Assert.That((string?)JObject.Parse(ReportJson.SerializeObject(restored))["StepType"]).IsEqualTo(stepType.ToString());
        await Assert.That(restored.StepText).IsEqualTo(step.StepText);

        // Regenerate text so that the stored StepText cannot hide a lost StepType.
        restored.OverriddenStepText = () => "restored text";
        restored.SetStepText();
        await Assert.That(restored.StepText).IsEqualTo($"{prefix} restored text");
    }

    [Test]
    public async Task Legacy_step_without_type_defaults_to_given()
    {
        var restored = ReportJson.DeserializeObject<Step>("{}");
        restored.OverriddenStepText = () => "legacy text";
        restored.SetStepText();
        await Assert.That(restored.StepText).IsEqualTo("Given legacy text");
    }

    [Test]
    [Arguments(0L, "0.00:00:00:0000000")]
    [Arguments(1L, "0.00:00:00:0000001")]
    [Arguments(937845678901L, "1.02:03:04:5678901")]
    [Arguments(long.MaxValue, "10675199.02:48:05:4775807")]
    public async Task Duration_round_trip_preserves_ticks_and_wire_format(long ticks, string wireValue)
    {
        var duration = TimeSpan.FromTicks(ticks);
        var json = JsonSerializer.Serialize(duration, DurationContext.TimeSpan);

        await Assert.That(json).IsEqualTo(ReportJson.SerializeObject(wireValue));
        await Assert.That(JsonSerializer.Deserialize(json, DurationContext.TimeSpan)).IsEqualTo(duration);

        var step = ReportJson.DeserializeObject<Step>($"{{\"TimeTaken\":{json}}}");
        await Assert.That(step.TimeTaken).IsEqualTo(duration);
        await Assert.That((string?)JObject.Parse(ReportJson.SerializeObject(step))["TimeTaken"]).IsEqualTo(wireValue);
    }

    [Test]
    [Arguments("\"not a duration\"")]
    [Arguments("\"\"")]
    [Arguments("\"00:00:01\"")]
    [Arguments("\"0.24:00:00:0000000\"")]
    [Arguments("\"10675199.02:48:05:4775808\"")]
    [Arguments("null")]
    [Arguments("123")]
    [Arguments("1.5")]
    [Arguments("true")]
    [Arguments("[]")]
    [Arguments("{}")]
    public async Task Invalid_or_null_duration_throws_serialization_exception(string json)
    {
        await Assert.That(() => JsonSerializer.Deserialize(json, DurationContext.TimeSpan)).ThrowsExactly<JsonException>();
        await Assert.That(() => ReportJson.DeserializeObject<Step>($"{{\"TimeTaken\":{json}}}")).ThrowsExactly<JsonException>();
    }

    private static void NoOp() { }
}
