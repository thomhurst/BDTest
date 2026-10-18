using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using BDTest.Attributes;
using BDTest.Metadata;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class TestMethodMetadataTests
{
    [Test]
    public void Core_cannot_reference_stack_discovery_types()
    {
        using var stream = File.OpenRead(typeof(BDTestBase).Assembly.Location);
        using var assembly = new PEReader(stream);
        var metadata = assembly.GetMetadataReader();
        var forbidden = metadata.TypeReferences.Select(metadata.GetTypeReference)
            .Where(type => metadata.GetString(type.Namespace) == "System.Diagnostics")
            .Select(type => metadata.GetString(type.Name))
            .Where(name => name is "StackTrace" or "StackFrame");
        Assert.That(forbidden, Is.Empty);
    }

    [Test]
    public async Task Metadata_is_cached_and_scenario_information_is_independent()
    {
        using var run = BDTestRun.Start();
        var fixture = new Harness(run);
        var method = typeof(Harness).GetMethod("Annotated", BindingFlags.NonPublic | BindingFlags.Instance);
        var values = new TestMethodMetadata[32];
        Parallel.For(0, values.Length, index => values[index] = TestMethodMetadata.Get(typeof(Harness), method));
        Assert.That(values.All(value => ReferenceEquals(value, values[0])), Is.True);
        Assert.That(CountingInformationAttribute.Instances, Is.Zero);

        fixture.Start(method);
        var first = await fixture.Build();
        Assert.That(CountingInformationAttribute.Instances, Is.EqualTo(1));
        first.Tags[0] = "changed";
        var firstInformation = (CountingInformationAttribute)first.CustomTestInformation[0];
        firstInformation.Value = "changed";
        firstInformation.Values[0] = "changed";
        firstInformation.Items[0] = "changed";
        var second = await fixture.Build();
        Assert.That(CountingInformationAttribute.Instances, Is.EqualTo(2));
        Assert.That(second.GetScenarioText(), Is.EqualTo("Exact private method"));
        Assert.That(second.Tags, Is.EqualTo(new[] { "original" }));
        var secondInformation = (CountingInformationAttribute)second.CustomTestInformation[0];
        Assert.That(secondInformation.Value, Is.EqualTo("original"));
        Assert.That(secondInformation.Values, Is.EqualTo(new[] { "original" }));
        Assert.That(secondInformation.Items, Is.EqualTo(new[] { "original" }));
        fixture.Finish();
        var fallback = await fixture.Build();
        Assert.That(fallback.GetScenarioText(), Is.EqualTo("Build"));
        Assert.That(fallback.CustomTestInformation, Is.Empty);
    }

    [Test]
    [ScenarioText("Must not leak from the stack")]
    [TestInformation("Must not leak")]
    [BDTestRetry(3)]
    public async Task Custom_harness_uses_caller_text_and_file_without_guessing_attributes()
    {
        using var run = BDTestRun.Start();
        var fixture = new Harness(run);
        var scenario = await fixture.Build();
        Assert.That(scenario.GetScenarioText(), Is.EqualTo("Build"));
        Assert.That(scenario.FileName, Does.EndWith("TestMethodMetadataTests.cs"));
        Assert.That(scenario.CustomTestInformation, Is.Empty);
        Assert.That(scenario.TestMetadata.RetryCount, Is.Null);
        Assert.That((await fixture.When(() => { }).Then(() => { })
            .WithScenarioText("Explicit harness text").BDTestAsync()).GetScenarioText(), Is.EqualTo("Explicit harness text"));
    }

    [Test]
    public void Inherited_method_metadata_keeps_concrete_fixture_defaults()
    {
        var method = typeof(BaseFixture).GetMethod(nameof(BaseFixture.Test));
        var first = TestMethodMetadata.Get(typeof(FirstFixture), method);
        var second = TestMethodMetadata.Get(typeof(SecondFixture), method);
        Assert.That(first.RetryCount, Is.EqualTo(1));
        Assert.That(second.RetryCount, Is.EqualTo(2));
        Assert.That(first.CopyTags(), Is.EqualTo(new[] { "first" }));
        Assert.That(second.CopyTags(), Is.EqualTo(new[] { "second" }));
        Assert.That(first.CopyParameters(), Is.EqualTo(new[] { "value" }));
    }

    private class BaseFixture { public void Test(int value) { } }
    [BDTestRetry(1), BDTestTag("first")]
    private sealed class FirstFixture : BaseFixture;
    [BDTestRetry(2), BDTestTag("second")]
    private sealed class SecondFixture : BaseFixture;

    private sealed class Harness(BDTestRun run) : BDTestBase
    {
        public override BDTestRun TestRun => run;
        public void Start(MethodInfo method) => BeginTest(Guid.NewGuid().ToString("N"), method);
        public void Finish() => EndTest();
        public Task<Scenario> Build() => When(() => { }).Then(() => { }).BDTestAsync();

        [ScenarioText("Exact private method"), BDTestTag("original"), CountingInformation(Values = new[] { "original" })]
        private void Annotated() { }
    }

    private sealed class CountingInformationAttribute : TestInformationAttribute
    {
        public static int Instances;
        public string Value = "original";
        public string[] Values { get; set; }
        public List<string> Items = new() { "original" };
        public CountingInformationAttribute() : base("information") => Interlocked.Increment(ref Instances);
    }
}
