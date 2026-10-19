using BDTest.Razor.Reports.Helpers;
using BDTest.Razor.Reports.Models;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class FailureGroupTests
{
    private static Scenario Failed(string id, string type, string message, string stackTrace, string step = "Then it works") =>
        ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(new
        {
            Guid = id, Status = "Failed", ScenarioText = id, StoryText = "As a user\nI want to buy items\nSo that I get them",
            Steps = new object[]
            {
                new { StepText = "Given a thing", Status = "Passed" },
                new { StepText = step, Status = "Failed", Exception = new { TypeName = type, Message = message, StackTrace = stackTrace, AsString = message } }
            }
        }));

    [Test]
    public async Task Location_uses_the_first_frame_with_a_source_file()
    {
        var exception = new ExceptionWrapper
        {
            StackTrace = "   at Framework.Assert(String)\n   at CheckoutSteps.ThenTheStatusIs(Int32 expected) in C:\\src\\Steps\\CheckoutSteps.cs:line 84\n   at Next() in C:\\Next.cs:line 1"
        };

        await Assert.That(FailureGroup.GetLocation((exception).ToResult())).IsEqualTo("CheckoutSteps.cs:line 84 · CheckoutSteps.ThenTheStatusIs");
    }

    [Test]
    public async Task Location_falls_back_to_the_first_method_and_ignores_text_that_is_not_a_frame()
    {
        await Assert.That(FailureGroup.GetLocation((new ExceptionWrapper { StackTrace = "noise\n   at Some.Method()" }).ToResult())).IsEqualTo("Some.Method");
        await Assert.That(FailureGroup.GetLocation((new ExceptionWrapper { StackTrace = "no frames here" }).ToResult())).IsNull();
        await Assert.That(FailureGroup.GetLocation(null)).IsNull();
    }

    [Test]
    public async Task Location_only_reads_the_start_of_very_large_stack_traces()
    {
        var trace = string.Concat(Enumerable.Repeat("   at A.B()\n", 100_000)) + "   at Late.Frame() in Late.cs:line 9";
        await Assert.That(FailureGroup.GetLocation((new ExceptionWrapper { StackTrace = trace }).ToResult())).IsEqualTo("A.B");
    }

    [Test]
    public async Task Failures_group_by_message_and_location_type_or_step()
    {
        const string trace = "   at Steps.Then() in Steps.cs:line 4";
        var scenarios = new[]
        {
            Failed("one", "AssertionException", "Expected 201", trace),
            Failed("two", "AssertionException", "Expected 201", trace),
            Failed("three", "AssertionException", "Expected 404", trace, "Then it is missing"),
            Failed("four", "TimeoutException", "Timed out", "   at Client.Send() in Client.cs:line 9")
        };

        var byLocation = FailureGroup.Build((scenarios).ToResults(), FailureGroup.ByExceptionAndLocation);
        await Assert.That(byLocation.Select(group => group.Scenarios.Count)).IsEquivalentTo(new[] { 2, 1, 1 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(byLocation[0].Title).IsEqualTo("Expected 201");
        await Assert.That(byLocation[0].Location).IsEqualTo("Steps.cs:line 4 · Steps.Then");

        var byType = FailureGroup.Build((scenarios).ToResults(), FailureGroup.ByExceptionType);
        await Assert.That(byType.Select(group => group.Title)).IsEquivalentTo(new[] { "AssertionException", "TimeoutException" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);

        var byStep = FailureGroup.Build((scenarios).ToResults(), FailureGroup.ByFailingStep);
        await Assert.That(byStep.Select(group => group.Title)).IsEquivalentTo(new[] { "Then it works", "Then it is missing" }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(FailureGroup.FailingStepNumber((scenarios[0]).ToResult())).IsEqualTo(2);
    }

    [Test]
    public async Task Outline_failures_use_the_failing_step_of_their_failed_example()
    {
        var outline = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(new
        {
            Guid = "outline", Status = "Failed", IsOutline = true, ScenarioText = "Outline", Steps = Array.Empty<object>(),
            Examples = new object[]
            {
                new { Guid = "row-1", Status = "Passed", Steps = new object[] { new { StepText = "Given row 1", Status = "Passed" } } },
                new
                {
                    Guid = "row-2", Status = "Failed", Steps = new object[]
                    {
                        new { StepText = "Given row 2", Status = "Passed" },
                        new { StepText = "Then row 2 fails", Status = "Failed", Exception = new { TypeName = "E", Message = "boom", AsString = "boom" } }
                    }
                }
            }
        }));

        await Assert.That(FailureGroup.FailingStep((outline).ToResult())?.StepText).IsEqualTo("Then row 2 fails");
        await Assert.That(FailureGroup.FailingStepNumber((outline).ToResult())).IsEqualTo(2);
        await Assert.That(FailureGroup.Build((new[] { outline }).ToResults(), FailureGroup.ByFailingStep).Single().Title).IsEqualTo("Then row 2 fails");
    }

    [Test]
    public async Task Passed_scenarios_are_not_failures()
    {
        var passed = ReportJson.DeserializeObject<Scenario>("""{ "Guid": "p", "Status": "Passed", "Steps": [] }""");
        await Assert.That(FailureGroup.Build((new[] { passed }).ToResults(), FailureGroup.ByExceptionAndLocation)).IsEmpty();
    }

    [Test]
    [Arguments("As a customer\r\nI want to buy items\r\nSo that I receive my order\r\n", "Buy items", "As a customer, I want to buy items, so that I receive my order")]
    [Arguments("Shopping basket", "Shopping basket", "Shopping basket")]
    [Arguments("Story Text Not Defined", "No story", "")]
    [Arguments(null, "No story", "")]
    public async Task Stories_have_a_short_title_and_a_sentence(string? story, string title, string sentence)
    {
        await Assert.That(ReportUi.StoryTitle(story)).IsEqualTo(title);
        await Assert.That(ReportUi.StorySentence(story)).IsEqualTo(sentence);
    }

    [Test]
    [Arguments(0, "0 s")]
    [Arguments(210, "0.21 s")]
    [Arguments(2_400, "2.4 s")]
    [Arguments(102_000, "1m 42s")]
    [Arguments(3_780_000, "1h 3m")]
    public async Task Durations_are_short(int milliseconds, string expected)
    {
        using var culture = new CultureScope("en-GB");
        await Assert.That(TimeSpan.FromMilliseconds(milliseconds).ToShortDuration()).IsEqualTo(expected);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly System.Globalization.CultureInfo _previous = System.Globalization.CultureInfo.CurrentCulture;

        public CultureScope(string name) => System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(name);

        public void Dispose() => System.Globalization.CultureInfo.CurrentCulture = _previous;
    }
}
