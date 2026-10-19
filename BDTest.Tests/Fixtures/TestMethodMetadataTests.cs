using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using BDTest.Attributes;
using BDTest.Metadata;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class TestMethodMetadataTests
{
    [Test]
    public async Task Core_cannot_reference_stack_discovery_types()
    {
        using var stream = File.OpenRead(typeof(BDTestBase).Assembly.Location);
        using var assembly = new PEReader(stream);
        var metadata = assembly.GetMetadataReader();
        var forbidden = metadata.TypeReferences.Select(metadata.GetTypeReference)
            .Where(type => metadata.GetString(type.Namespace) == "System.Diagnostics")
            .Select(type => metadata.GetString(type.Name))
            .Where(name => name is "StackTrace" or "StackFrame");
        await Assert.That(forbidden).IsEmpty();
    }

    [Test]
    public async Task Metadata_is_cached_and_scenario_information_is_independent()
    {
        using var run = BDTestRun.Start();
        var fixture = new Harness(run);
        var method = typeof(Harness).GetMethod("Annotated", BindingFlags.NonPublic | BindingFlags.Instance);
        var values = new TestMethodMetadata[32];
        Parallel.For(0, values.Length, index => values[index] = TestMethodMetadata.Get(typeof(Harness), method));
        await Assert.That(values.All(value => ReferenceEquals(value, values[0]))).IsTrue();
        await Assert.That(CountingInformationAttribute.Instances).IsZero();

        fixture.Start(Required(method));
        var first = await fixture.Build();
        await Assert.That(CountingInformationAttribute.Instances).IsEqualTo(1);
        first.Tags[0] = "changed";
        var firstInformation = (CountingInformationAttribute)Required(first.CustomTestInformation)[0];
        firstInformation.Value = "changed";
        firstInformation.Values[0] = "changed";
        firstInformation.Items[0] = "changed";
        var second = await fixture.Build();
        await Assert.That(CountingInformationAttribute.Instances).IsEqualTo(2);
        await Assert.That(second.GetScenarioText()).IsEqualTo("Exact private method");
        await Assert.That(second.Tags).IsEquivalentTo(new[] { "original" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        var secondInformation = (CountingInformationAttribute)Required(second.CustomTestInformation)[0];
        await Assert.That(secondInformation.Value).IsEqualTo("original");
        await Assert.That(secondInformation.Values).IsEquivalentTo(new[] { "original" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(secondInformation.Items).IsEquivalentTo(new[] { "original" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        fixture.Finish();
        var fallback = await fixture.Build();
        await Assert.That(fallback.GetScenarioText()).IsEqualTo("Build");
        await Assert.That(fallback.CustomTestInformation).IsEmpty();
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
        await Assert.That(scenario.GetScenarioText()).IsEqualTo("Build");
        await Assert.That(scenario.FileName).EndsWith("TestMethodMetadataTests.cs");
        await Assert.That(scenario.CustomTestInformation).IsEmpty();
        await Assert.That(Required(scenario.TestMetadata).RetryCount).IsNull();
        await Assert.That((await fixture.When(() => { }).Then(() => { })
            .WithScenarioText("Explicit harness text").BDTestAsync()).GetScenarioText()).IsEqualTo("Explicit harness text");
    }

    [Test]
    public async Task Inherited_method_metadata_keeps_concrete_fixture_defaults()
    {
        var method = typeof(BaseFixture).GetMethod(nameof(BaseFixture.Test));
        var first = TestMethodMetadata.Get(typeof(FirstFixture), method);
        var second = TestMethodMetadata.Get(typeof(SecondFixture), method);
        await Assert.That(first.RetryCount).IsEqualTo(1);
        await Assert.That(second.RetryCount).IsEqualTo(2);
        await Assert.That(first.CopyTags()).IsEquivalentTo(new[] { "first" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(second.CopyTags()).IsEquivalentTo(new[] { "second" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(first.CopyParameters()).IsEquivalentTo((new[] { "value" }).Cast<string?>(), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
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
        public Task<BDTest.Contracts.ScenarioResult> Build() => When(() => { }).Then(() => { }).BDTestAsync();

        [ScenarioText("Exact private method"), BDTestTag("original"), CountingInformation(Values = new[] { "original" })]
        private void Annotated() { }
    }

    private sealed class CountingInformationAttribute : TestInformationAttribute
    {
        public static int Instances;
        public string Value = "original";
        public string[] Values { get; set; } = [];
        public List<string> Items = new() { "original" };
        public CountingInformationAttribute() : base("information") => Interlocked.Increment(ref Instances);
    }
}
