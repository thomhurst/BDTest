using TUnit.Assertions.Enums;
using System.Text;
using BDTest.Maps;
using BDTest.ReportGenerator.RazorServer;
using Newtonsoft.Json;

namespace BDTest.ReportGenerator.Tests;

public class JsonStreamTests
{
    [Test]
    public async Task ReportStreamContainsCompleteUtf8JsonWithoutABom()
    {
        var model = new BDTestOutputModel
        {
            Id = "stream-report",
            Tag = "Quotes: \" and Unicode: café 日本語 🧪\n" + new string('x', 100_000)
        };
        using var stream = model.AsStream();

        await Assert.That(stream.CanRead).IsTrue();
        await Assert.That(stream.Position).IsZero();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        await Assert.That(copy.ToArray())
            .IsEquivalentTo(Encoding.UTF8.GetBytes(ReportJson.SerializeObject(model)), CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(null)]
    [Arguments("café \"quoted\"\n")]
    public async Task GenericStreamSerializesNullAndStringValues(string? value)
    {
        using var stream = value.AsStream<string?>();
        using var reader = new StreamReader(stream);
        await Assert.That(reader.ReadToEnd()).IsEqualTo(ReportJson.SerializeObject(value));
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("{\"value\":\"café 🧪\"}")]
    public async Task StringOverloadPreservesRawText(string? value)
    {
        using var stream = value.AsStream();
        await Assert.That(stream.Position).IsZero();
        using var reader = new StreamReader(stream);
        await Assert.That(reader.ReadToEnd()).IsEqualTo(value ?? string.Empty);
    }
}
