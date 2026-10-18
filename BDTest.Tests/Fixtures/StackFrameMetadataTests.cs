using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using BDTest.Attributes;
using BDTest.Helpers;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class StackFrameMetadataTests
{
    [Test]
    public void Scenario_text_skips_unresolved_frames_and_keeps_attribute_precedence_and_parameters()
    {
        var result = ScenarioTextHelper.GetScenarioTextWithParameters(Method(nameof(Resolved_metadata)),
            nameof(Caller_with_parameters), Frames(nameof(Caller_with_parameters), nameof(Stack_metadata)));

        Assert.That(result.ScenarioText.Scenario, Is.EqualTo("Stack scenario"));
        Assert.That(result.Parameters, Is.EqualTo(new[] { "stackValue" }));
    }

    [Test]
    public void Scenario_text_uses_resolved_caller_frame_and_parameters()
    {
        var result = ScenarioTextHelper.GetScenarioTextWithParameters(Method(nameof(Resolved_metadata)),
            nameof(Caller_with_parameters), Frames(nameof(Caller_with_parameters)));

        Assert.That(result.ScenarioText.Scenario, Is.EqualTo("Caller with parameters"));
        Assert.That(result.Parameters, Is.EqualTo(new[] { "callerValue" }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Scenario_text_falls_back_to_resolved_test_method(bool unresolvedFrame)
    {
        var result = ScenarioTextHelper.GetScenarioTextWithParameters(Method(nameof(Resolved_metadata)),
            nameof(Caller_with_parameters), UnresolvedFrames(unresolvedFrame));

        Assert.That(result.ScenarioText.Scenario, Is.EqualTo("Resolved scenario"));
        Assert.That(result.Parameters, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Scenario_text_falls_back_to_caller_name(bool unresolvedFrame)
    {
        var result = ScenarioTextHelper.GetScenarioTextWithParameters(null,
            nameof(Caller_with_parameters), UnresolvedFrames(unresolvedFrame));

        Assert.That(result.ScenarioText.Scenario, Is.EqualTo("Caller with parameters"));
        Assert.That(result.Parameters, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Scenario_text_keeps_missing_metadata_message(bool unresolvedFrame)
    {
        var result = ScenarioTextHelper.GetScenarioTextWithParameters(null, null, UnresolvedFrames(unresolvedFrame));

        Assert.That(result.ScenarioText.Scenario,
            Is.EqualTo("No Scenario Text found (Use attribute [ScenarioText(\"...\")] on your tests"));
        Assert.That(result.Parameters, Is.Empty);
    }

    [Test]
    public void Retry_uses_resolved_test_method_before_stack_attributes()
    {
        var test = new RetryStory();
        var result = RetryAttributeHelper.GetBDTestRetryAttribute(
            ScenarioFor(test, nameof(RetryStory.Retry_method)), Frames(nameof(Stack_metadata)));

        Assert.That(result.Count, Is.EqualTo(2));
    }

    [Test]
    public void Retry_skips_unresolved_frames_and_uses_stack_attribute_before_story()
    {
        var test = new RetryStory();
        var result = RetryAttributeHelper.GetBDTestRetryAttribute(
            ScenarioFor(test, nameof(RetryStory.Unannotated_method)), Frames(nameof(Stack_metadata)));

        Assert.That(result.Count, Is.EqualTo(3));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Retry_falls_back_to_story_attribute(bool unresolvedFrame)
    {
        var test = new RetryStory();
        var result = RetryAttributeHelper.GetBDTestRetryAttribute(
            ScenarioFor(test, nameof(RetryStory.Unannotated_method)), UnresolvedFrames(unresolvedFrame));

        Assert.That(result.Count, Is.EqualTo(4));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Retry_returns_null_without_metadata(bool unresolvedFrame)
    {
        Assert.That(RetryAttributeHelper.GetBDTestRetryAttribute(null, UnresolvedFrames(unresolvedFrame)), Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Retry_falls_back_to_assembly_attribute(bool unresolvedFrame)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("RetryFallback"), AssemblyBuilderAccess.Run);
        assembly.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(BDTestRetryAttribute).GetConstructor(new[] { typeof(int) }), new object[] { 5 }));
        var type = assembly.DefineDynamicModule("Tests").DefineType("AssemblyRetryStory", TypeAttributes.Public, typeof(BDTestBase));
        type.DefineDefaultConstructor(MethodAttributes.Public);
        var test = (BDTestBase)Activator.CreateInstance(type.CreateType());

        var result = RetryAttributeHelper.GetBDTestRetryAttribute(
            ScenarioFor(test, "Missing_method"), UnresolvedFrames(unresolvedFrame));

        Assert.That(result.Count, Is.EqualTo(5));
    }

    [Test]
    public void Test_information_skips_unresolved_frames_and_keeps_all_stack_attributes()
    {
        var result = TestInformationAttributeHelper.GetTestInformationAttributes(
            Method(nameof(Resolved_metadata)), Frames(nameof(Caller_with_parameters), nameof(Stack_metadata)));

        Assert.That(result.Select(attribute => attribute.Information), Is.EqualTo(new[] { "Stack information", "More stack information" }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Test_information_falls_back_to_resolved_test_method(bool unresolvedFrame)
    {
        var result = TestInformationAttributeHelper.GetTestInformationAttributes(
            Method(nameof(Resolved_metadata)), UnresolvedFrames(unresolvedFrame));

        Assert.That(result.Select(attribute => attribute.Information), Is.EqualTo(new[] { "Resolved information" }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Test_information_returns_empty_without_metadata(bool unresolvedFrame)
    {
        Assert.That(TestInformationAttributeHelper.GetTestInformationAttributes(null, UnresolvedFrames(unresolvedFrame)), Is.Empty);
    }

    private static MethodInfo Method(string name) =>
        typeof(StackFrameMetadataTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);

    private static StackFrame[] UnresolvedFrames(bool unresolvedFrame) =>
        unresolvedFrame ? new StackFrame[] { new MetadataFrame(null) } : Array.Empty<StackFrame>();

    private static IEnumerable<StackFrame> Frames(params string[] methods)
    {
        yield return new MetadataFrame(null);
        foreach (var name in methods)
        {
            yield return new MetadataFrame(Method(name));
            yield return new MetadataFrame(null);
        }
    }

    private static Scenario ScenarioFor(BDTestBase test, string caller) => new()
    {
        RuntimeInformation = new BDTestRuntimeInformation { BdTestBase = test, CallerMember = caller }
    };

    private sealed class MetadataFrame(MethodBase method) : StackFrame
    {
        public override MethodBase GetMethod() => method;
    }

    [ScenarioText("Stack scenario")]
    [BDTestRetry(3)]
    [TestInformation("Stack information")]
    [TestInformation("More stack information")]
    private static void Stack_metadata(int stackValue) { }

    [ScenarioText("Resolved scenario")]
    [TestInformation("Resolved information")]
    private static void Resolved_metadata() { }

    private static void Caller_with_parameters(string callerValue) { }

    [BDTestRetry(4)]
    private sealed class RetryStory : BDTestBase
    {
        [BDTestRetry(2)]
        public void Retry_method() { }

        public void Unannotated_method() { }
    }
}
