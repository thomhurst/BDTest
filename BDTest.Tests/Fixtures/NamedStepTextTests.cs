using System.Globalization;
using System.Reflection;
using System.Threading;
using BDTest;
using BDTest.Helpers;
using BDTest.Interfaces;
using BDTest.Tests.Helpers;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class NamedStepTextTests : RunTestBase
{
    [Test]
    public async Task NamesSurviveReorderingAndFormatsUseInvariantCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            await Assert.That(Render(nameof(Amount), [() => 12.5m, () => "Alice"]))
                .IsEqualTo("Alice: 12.50 / 12.5");
            await Assert.That(Render(nameof(Reordered), [() => "Alice", () => 12.5m]))
                .IsEqualTo("Alice: 12.50 / 12.5");
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Test]
    public async Task RepeatedPlaceholdersReadAnArgumentOnce()
    {
        var reads = 0;
        await Assert.That(Render(nameof(Amount), [() => { reads++; return 12.5m; }, () => "Alice"]))
            .IsEqualTo("Alice: 12.50 / 12.5");
        await Assert.That(reads).IsEqualTo(1);
    }

    [Test]
    public async Task ConverterWinsOverFormatsAndRunsOnce()
    {
        var converter = new CountingConverter();
        Options.RegisterCustomStringConverter(converter);
        var method = typeof(NamedStepTextTests).GetMethod(nameof(Amount), BindingFlags.NonPublic | BindingFlags.Static)!;
        var text = StepTextHelper.GetStepText(method, [() => 12.5m, () => "Alice"], run: TestRun);
        await Assert.That(text).IsEqualTo("Alice: {converted} / {converted}");
        await Assert.That(converter.Calls).IsEqualTo(1);
    }

    [Test]
    public async Task InvalidOverridesAreDiagnosedAndIgnored()
    {
        var diagnostics = new List<string>();
        await Assert.That(Render(nameof(InvalidOverrides), [() => "actual"], diagnostics.Add)).IsEqualTo("actual");
        await Assert.That(diagnostics.Count).IsEqualTo(2);
    }

    [Test]
    public async Task BracesAndOverridesStayLiteral()
    {
        await Assert.That(Render(nameof(Braces), [() => "{unparsed}"]))
            .IsEqualTo("{value} {\"key\": 1} {unparsed} {");
        await Assert.That(Render(nameof(Override), [() => "secret"]))
            .IsEqualTo("{other}:{{literal}}=yes / {other}:{{literal}}=yes");
    }

    [Test]
    public async Task InvalidTemplatesAndFormatsAreNonFatal()
    {
        var diagnostics = new List<string>();
        await Assert.That(Render(nameof(Invalid), [() => 12], diagnostics.Add))
            .IsEqualTo("{missing} {0} <error: FormatException> {");
        await Assert.That(diagnostics.Count).IsEqualTo(3);
        await Assert.That(string.Join(" ", diagnostics)).Contains("missing").And.Contains("positional").And.Contains("FormatException");
    }

    [Test]
    public async Task CancellationTokensAreNeverReadOrRendered()
    {
        var diagnostics = new List<string>();
        await Assert.That(Render(nameof(Token), [() => throw new Exception("token reader"), () => 42], diagnostics.Add))
            .IsEqualTo("42 ");
        await Assert.That(diagnostics.Count).IsEqualTo(1);
        await Assert.That(Render(nameof(Token), [() => 42])).IsEqualTo("42 ");
    }

    [Test]
    public async Task FormattingErrorsDoNotFailExecutionOrMultiplyOnReportRefresh()
    {
        var scenario = await When(() => Invalid(12)).Then(() => Complete()).BDTestAsync();
        var step = Required(scenario.Steps)[0];
        await Assert.That(step.Status).IsEqualTo(Status.Passed);
        await Assert.That(Required(step.Diagnostics).Count).IsEqualTo(3);
        var first = BDTestUtil.GetScenarios(TestRun).Single();
        var second = BDTestUtil.GetScenarios(TestRun).Single();
        await Assert.That(Required(second.Steps)[0].Diagnostics).IsEquivalentTo(Required(first.Steps)[0].Diagnostics);
    }

    [Test]
    [Arguments(false), Arguments(true)]
    public async Task SkippedAndUnreachedTemplatesRemainSafeWithoutReadingArguments(bool skipped)
    {
        Options.Debug.ShouldSkipWhenStep = skipped;
        var reads = 0;
        var builder = skipped
            ? When(() => Invalid(12)).WithArgs(() => { reads++; return 12; }).Then(() => Complete())
            : When(() => Fail()).Then(() => Invalid(12)).WithArgs(() => { reads++; return 12; });
        if (skipped) await builder.BDTestAsync();
        else await Assert.That(async () => await builder.BDTestAsync()).ThrowsExactly<InvalidOperationException>();
        var step = Required(BDTestUtil.GetScenarios(TestRun).Single().Steps)[skipped ? 0 : 1];
        await Assert.That(reads).IsZero();
        await Assert.That(step.StepText).Contains("{missing} {0} {value:Q} {");
        await Assert.That(Required(step.Diagnostics).Count).IsEqualTo(2);
    }

    private static string Render(string name, IReadOnlyList<Func<object?>> arguments, Action<string>? diagnostic = null) =>
        StepTextHelper.GetStepText(typeof(NamedStepTextTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!, arguments, diagnostic: diagnostic);

    [StepText("{customer}: {amount:F2} / {amount}")]
    private static void Amount(decimal amount, string customer) { }
    [StepText("{customer}: {amount:F2} / {amount}")]
    private static void Reordered(string customer, decimal amount) { }
    [StepText("{{value}} {\"key\": 1} {value} {")]
    private static void Braces(string value) { }
    [StepText("{value} / {value:F2}", "value={other}:{{literal}}=yes")]
    private static void Override(string value) { }
#pragma warning disable BDT002 // Deliberately invalid templates exercise the runtime fallback.
    [StepText("{missing} {0} {value:Q} {")]
    private static void Invalid(int value) { }
    [StepText("{value} {token}")]
    private static void Token(CancellationToken token, int value) { }
    [StepText("{value}", new[] { "0:legacy", "missing=text" })]
    private static void InvalidOverrides(string value) { }
#pragma warning restore BDT002
    private static void Complete() { }
    private static void Fail() => throw new InvalidOperationException("expected");

    private sealed class CountingConverter : IStepTextStringConverter<decimal>
    {
        internal int Calls { get; private set; }
        public string ConvertToString(decimal value) { Calls++; return "{converted}"; }
    }
}
