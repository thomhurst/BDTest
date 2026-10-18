using System.Text;
using BDTest.Maps;
using BDTest.ReportGenerator.RazorServer;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.ReportGenerator.Tests;

public class JsonStreamTests
{
    [Test]
    public void ReportStreamContainsCompleteUtf8JsonWithoutABom()
    {
        var model = new BDTestOutputModel
        {
            Id = "stream-report",
            Tag = "Quotes: \" and Unicode: café 日本語 🧪\n" + new string('x', 100_000)
        };
        using var stream = model.AsStream();

        Assert.That(stream.CanRead, Is.True);
        Assert.That(stream.Position, Is.Zero);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        Assert.That(copy.ToArray(), Is.EqualTo(Encoding.UTF8.GetBytes(ReportJson.SerializeObject(model))));
    }

    [TestCase(null)]
    [TestCase("café \"quoted\"\n")]
    public void GenericStreamSerializesNullAndStringValues(string? value)
    {
        using var stream = value.AsStream<string?>();
        using var reader = new StreamReader(stream);
        Assert.That(reader.ReadToEnd(), Is.EqualTo(ReportJson.SerializeObject(value)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("{\"value\":\"café 🧪\"}")]
    public void StringOverloadPreservesRawText(string? value)
    {
        using var stream = value.AsStream();
        Assert.That(stream.Position, Is.Zero);
        using var reader = new StreamReader(stream);
        Assert.That(reader.ReadToEnd(), Is.EqualTo(value ?? string.Empty));
    }
}
