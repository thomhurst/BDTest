using System.Collections.Immutable;
using BDTest.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace BDTest.Analyzers.Tests;

public class AnalyzerTests
{
    [TestCase("void Test() { Given(() => {}); }", 1)]
    [TestCase("void Test() => Given(() => {});", 1)]
    [TestCase("Action Test() => () => Given(() => {});", 1)]
    [TestCase("Func<object> Test() => () => Given(() => {});", 0)]
    [TestCase("void Test() { Given(() => {}).When(() => {}).Then(() => {}); }", 1)]
    [TestCase("void Test() { _ = Given(() => {}).When(() => {}).Then(() => {}); }", 1)]
    [TestCase("void Test() { Given(() => {}).When(() => {}).Then(() => {}).BDTestAsync(); }", 1)]
    [TestCase("void Test() { _ = Given(() => {}).When(() => {}).Then(() => {}).BDTestAsync(); }", 1)]
    [TestCase("void Test() { Given(() => {}).When(() => {}).Then(() => {}).BDTestAsync().ConfigureAwait(false); }", 1)]
    [TestCase("async Task Test() { await Given(() => {}).When(() => {}).Then(() => {}); }", 0)]
    [TestCase("async Task Test() { await Given(() => {}).When(() => {}).Then(() => {}).BDTestAsync(); }", 0)]
    [TestCase("async Task Test() { await Given(() => {}).When(() => {}).Then(() => {}).BDTestAsync().ConfigureAwait(false); }", 0)]
    [TestCase("object Test() { return Given(() => {}); }", 0)]
    [TestCase("Task Test() => Given(() => {}).When(() => {}).Then(() => {}).BDTestAsync();", 0)]
    [TestCase("void Test() { var builder = Given(() => {}); }", 0)]
    [TestCase("void Test() { var builder = When(() => {}).Then(() => {}); builder.WithScenarioText(\"later\"); }", 0)]
    [TestCase("void Test() { var builder = When(() => {}); builder.WithStepTimeout(TimeSpan.FromSeconds(1)); }", 0)]
    [TestCase("void Test() { When(() => {}).WithStepTimeout(TimeSpan.FromSeconds(1)); }", 1)]
    [TestCase("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"BDTest\", \"BDT001\", Justification = \"Intentional discard\")] void Test() { Given(() => {}); }", 0)]
    [TestCase("void Use(object value) {} void Test() { Use(Given(() => {})); }", 0)]
    [TestCase("void Test() { var task = Given(() => {}).When(() => {}).Then(() => {}).BDTestAsync(); }", 0)]
    [TestCase("Task Given(int value) => Task.CompletedTask; void Test() { Given(1); }", 0)]
    [TestCase("class Other { public Other When() => this; public Task BDTestAsync() => Task.CompletedTask; } void Test() { new Other().When().BDTestAsync(); }", 0)]
    [TestCase("void Test() { ScenarioOutline(\"outline\", new[] { 1 }, value => Given(() => {}).When(() => {}).Then(() => {})); }", 1)]
    [TestCase("Task Test() => ScenarioOutline(\"outline\", new[] { 1 }, value => Given(() => {}).When(() => {}).Then(() => {})).BDTestAsync();", 0)]
    [TestCase("async Task Test() => Given(() => {}).When(() => {}).Then(() => {}).BDTestAsync();", 1)]
    [TestCase("async ValueTask Test() => Given(() => {}).When(() => {}).Then(() => {}).BDTestAsync();", 1)]
    [TestCase("Func<Task> Test() => async () => Given(() => {}).When(() => {}).Then(() => {}).BDTestAsync();", 1)]
    [TestCase("async Task Test() { async Task Local() => Given(() => {}).When(() => {}).Then(() => {}).BDTestAsync(); await Local(); }", 1)]
    [TestCase("async Task Test() => await Given(() => {}).When(() => {}).Then(() => {}).BDTestAsync();", 0)]
    [TestCase("Func<Task> Test() => () => Given(() => {}).When(() => {}).Then(() => {}).BDTestAsync();", 0)]
    public async Task Execution_only_flags_definite_discards(string members, int count) =>
        await AssertRule(members, "BDT001", count);

    [TestCase("void Test() { var step = Given(() => { Task.Delay(1); }); }", 1)]
    [TestCase("void Test() { var step = Given((Action)(() => Task.Delay(1))); }", 1)]
    [TestCase("void Test() { var step = Given(() => { _ = Task.Delay(1); }); }", 1)]
    [TestCase("void Test() { var step = Given(() => { Task.Delay(1).ConfigureAwait(false); }); }", 1)]
    [TestCase("void Test() { var step = Given((Action)(async () => await Task.Delay(1))); }", 1)]
    [TestCase("async void Step() { await Task.Delay(1); } void Test() { var step = Given(Step); }", 1)]
    [TestCase("ValueTask Step() => default; void Test() { var step = Given((Action)(() => Step())); }", 1)]
    [TestCase("ValueTask<int> Step() => default; void Test() { var step = Given(() => { Step(); }); }", 1)]
    [TestCase("void Test() { var step = Given(() => Task.Delay(1)); }", 0)]
    [TestCase("void Test() { var step = Given((Func<Task>)(async () => await Task.Delay(1))); }", 0)]
    [TestCase("void Test() { var step = Given((Func<ValueTask>)(async () => await Task.Delay(1))); }", 0)]
    [TestCase("ValueTask Step() => default; void Test() { var step = Given(Step); }", 0)]
    [TestCase("ValueTask<int> Step() => default; void Test() { var step = Given(Step); }", 0)]
    [TestCase("void Test() { var step = Given(token => Task.Delay(1, token)); }", 0)]
    [TestCase("ValueTask Step(CancellationToken token) => default; void Test() { var step = Given(Step); }", 0)]
    [TestCase("ValueTask<int> Step(CancellationToken token) => default; void Test() { var step = Given(Step); }", 0)]
    [TestCase("void Test() { var step = Given(() => { var task = Task.Delay(1); }); }", 0)]
    [TestCase("void Test() { var step = Given(() => { Action nested = () => { Task.Delay(1); }; }); }", 0)]
    [TestCase("class Other { public int GetAwaiter() => 1; } void Test() { var step = Given(() => { new Other(); }); }", 0)]
    [TestCase("void Given(Action step, int other) {} void Test() { Given(() => { Task.Delay(1); }, 1); }", 0)]
    [TestCase("async void Step() { await Task.Delay(1); } void Test() { var step = Given(() => Step()); }", 1)]
    [TestCase("async void Step() { await Task.Delay(1); } void Test() { var step = Given(() => { Step(); }); }", 1)]
    [TestCase("async void Step() { await Task.Delay(1); } void Test() { var step = Given(() => { if (true) Step(); }); }", 1)]
    [TestCase("async void Step() { await Task.Delay(1); } void Test() { var step = Given(() => { Action nested = () => Step(); }); }", 0)]
    [TestCase("async void Step() { await Task.Delay(1); } void Test() { var step = Given(() => { void Local() => Step(); }); }", 0)]
    public async Task Async_steps_respect_bound_delegate(string members, int count) =>
        await AssertRule(members, "BDT004", count);

    [TestCase("[StepText(\"{value}\")] void Step(int value) {}", 0)]
    [TestCase("[StepText(\"{missing}\")] void Step(int value) {}", 1)]
    [TestCase("[StepText(\"{Value}\")] void Step(int value) {}", 1)]
    [TestCase("[StepText(\"{value:N2}\")] void Step(decimal value) {}", 0)]
    [TestCase("[StepText(\"{0}\")] void Step(int value) {}", 1)]
    [TestCase("[StepText(\"{0,10:N2}\")] void Step(int value) {}", 1)]
    [TestCase("[StepText(\"{value}\", \"0:old\")] void Step(int value) {}", 1)]
    [TestCase("[StepText(\"{value}\", \"missing=text\")] void Step(int value) {}", 1)]
    [TestCase("[StepText(\"{value}\", \"value:old\")] void Step(int value) {}", 1)]
    [TestCase("[StepText(\"{value}\", \"value={unknown}\")] void Step(int value) {}", 0)]
    [TestCase("[StepText(\"{value}\", \"value=\")] void Step(int value) {}", 0)]
    [TestCase("[StepText(\"{value}\", \"value=one=two\")] void Step(int value) {}", 0)]
    [TestCase("[StepText(\"{{unknown}} {{{value}}}\")] void Step(int value) {}", 0)]
    [TestCase("[StepText(\"unmatched { brace and } }\")] void Step() {}", 0)]
    [TestCase("[StepText(\"{not a placeholder}\")] void Step() {}", 0)]
    [TestCase("[StepText(\"{\\\"name\\\": \\\"value\\\"}\")] void Step() {}", 0)]
    [TestCase("[StepText(\"{value}\", (string[])null)] void Step(int value) {}", 1)]
    [TestCase("[StepText(\"{value}\", new string[] { null })] void Step(int value) {}", 1)]
    [TestCase("[StepText(\"{value}\", new string[0])] void Step(int value) {}", 0)]
    [TestCase("[StepText(\"{token}\")] Task Step(CancellationToken token) => Task.CompletedTask;", 1)]
    [TestCase("[StepText(\"{value}\")] Task Step(int value, CancellationToken token) => Task.CompletedTask; void Test() { var step = Given(() => Step(1, CancellationToken.None)); }", 0)]
    [TestCase("[StepText(\"{token}\")] Task Step(int value, CancellationToken token) => Task.CompletedTask; void Test() { var step = Given(() => Step(1, CancellationToken.None)); }", 1)]
    [TestCase("[StepText(\"{value}\", \"token=hidden\")] Task Step(int value, CancellationToken token) => Task.CompletedTask;", 1)]
    public async Task Step_text_matches_named_templates_and_visible_parameters(string members, int count) =>
        await AssertRule(members, "BDT002", count);

    [TestCase("", 1)]
    [TestCase("AsA = \"user\", IWant = \"tests\"", 1)]
    [TestCase("AsA = \"user\", AsAn = \"admin\", IWant = \"tests\", SoThat = \"report\"", 1)]
    [TestCase("AsA = \"\", IWant = \"tests\", SoThat = \"report\"", 1)]
    [TestCase("AsA = \"user\", IWant = \"tests\", SoThat = \"report\"", 0)]
    [TestCase("AsAn = \"admin\", IWant = \"tests\", SoThat = \"report\"", 0)]
    public async Task Story_requires_complete_nonconflicting_arguments(string arguments, int count)
    {
        var source = "[Story(" + arguments + ")] class StoryFixture {}";
        var diagnostics = await Analyze(CreateCompilation(Prefix + source));
        Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(Enumerable.Repeat("BDT003", count)));
    }

    [Test]
    public async Task Unrelated_attributes_are_ignored()
    {
        var source = """
            using System;
            [Story] class Fixture {
                [StepText("{10}")] void Step() {}
            }
            class StoryAttribute : Attribute {}
            class StepTextAttribute : Attribute { public StepTextAttribute(string text) {} }
            """;
        Assert.That(await Analyze(CreateCompilation(source)), Is.Empty);
    }

    [Test]
    public async Task Generator_and_analyzer_coexist_without_duplicate_diagnostics()
    {
        var compilation = CreateCompilation(Source("void Step() {} void Test() { Given(() => Step()).When(() => Step()).Then(() => Step()); }"));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new StepMetadataGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var generated, out var generatorDiagnostics);
        Assert.That(generatorDiagnostics, Is.Empty);
        var diagnostics = await Analyze(generated);
        Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "BDT001" }));
    }

    [Test]
    public async Task Generator_and_analyzer_report_invalid_template_once()
    {
        var compilation = CreateCompilation(Source("[StepText(\"{missing}\")] void Step(int value) {}"));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new StepMetadataGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var generated, out var generatorDiagnostics);
        Assert.That(generatorDiagnostics.Select(d => d.Id), Is.EqualTo(new[] { "BDT002" }));
        Assert.That(await Analyze(generated), Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Inherited_templates_validate_selected_method_parameter_names(bool withGenerator)
    {
        var source = Prefix + """
            class Parent : BDTestBuilder {
                public Parent(BDTest.BDTestRun run) : base(run) {}
                [StepText("{original}")] protected virtual void Step(int original) {}
            }
            class Consumer : Parent {
                public Consumer(BDTest.BDTestRun run) : base(run) {}
                protected override void Step(int renamed) {}
                void Test() { var builder = Given(() => Step(1)); }
            }
            """;
        Compilation compilation = CreateCompilation(source);
        AssertCompiles(compilation);
        var diagnostics = ImmutableArray<Diagnostic>.Empty;
        if (withGenerator)
        {
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new StepMetadataGenerator());
            driver.RunGeneratorsAndUpdateCompilation(compilation, out compilation, out diagnostics);
        }
        diagnostics = diagnostics.AddRange(await Analyze(compilation));
        Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "BDT002" }));
        Assert.That(diagnostics.Single().GetMessage(), Does.Contain("original"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Imported_templates_are_validated_at_step_binding(bool withGenerator)
    {
        var library = CreateCompilation(Prefix + """
            public static class Library {
                [StepText("{missing}")] public static void Step(int value) {}
            }
            """).WithAssemblyName("ExternalSteps");
        using var image = new MemoryStream();
        var emit = library.Emit(image);
        Assert.That(emit.Success, Is.True, string.Join(Environment.NewLine, emit.Diagnostics));
        Compilation compilation = CreateCompilation(Source("void Test() { var step = Given(() => Library.Step(1)); }"))
            .AddReferences(MetadataReference.CreateFromImage(image.ToArray()));
        AssertCompiles(compilation);
        var diagnostics = ImmutableArray<Diagnostic>.Empty;
        if (withGenerator)
        {
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new StepMetadataGenerator());
            driver.RunGeneratorsAndUpdateCompilation(compilation, out compilation, out diagnostics);
        }
        diagnostics = diagnostics.AddRange(await Analyze(compilation));
        Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "BDT002" }));
        Assert.That(diagnostics.Single().GetMessage(), Does.Contain("missing"));
    }

    private static async Task AssertRule(string members, string id, int count)
    {
        var compilation = CreateCompilation(Source(members));
        AssertCompiles(compilation);
        var diagnostics = await Analyze(compilation);
        Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(Enumerable.Repeat(id, count)),
            string.Join(Environment.NewLine, diagnostics));
    }

    internal static string Source(string members) => Prefix + "class Consumer : BDTestBuilder { public Consumer(BDTest.BDTestRun run) : base(run) {} " + members + " }";
    internal const string Prefix = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using BDTest;
        using BDTest.Test;

        """;

    internal static readonly ImmutableArray<MetadataReference> References =
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? throw new InvalidOperationException("Missing runtime assembly paths")).Split(Path.PathSeparator)
            .Append(typeof(BDTest.BDTestBuilder).Assembly.Location).Distinct()
            .Select(p => MetadataReference.CreateFromFile(p)).ToImmutableArray<MetadataReference>();

    internal static CSharpCompilation CreateCompilation(string source) => CSharpCompilation.Create("AnalyzerConsumer",
        new[] { CSharpSyntaxTree.ParseText(source, path: "Consumer.cs") }, References,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    internal static Task<ImmutableArray<Diagnostic>> Analyze(Compilation compilation) =>
        compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new BDTestAnalyzer())).GetAnalyzerDiagnosticsAsync();

    internal static void AssertCompiles(Compilation compilation) => Assert.That(
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error), Is.Empty);
}
