using System.Collections.Immutable;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace BDTest.Analyzers.Tests;

public class CodeFixTests
{
    [TestCase("async Task Test() { Given(() => {}).When(() => {}).Then(() => {}); }", "BDT001")]
    [TestCase("async Task Test() { _ = Given(() => {}).When(() => {}).Then(() => {}).BDTestAsync(); }", "BDT001")]
    [TestCase("async Task Test() { Given(() => {}).When(() => {}).Then(() => {}).BDTestAsync().ConfigureAwait(false); }", "BDT001")]
    [TestCase("void Test() { var step = Given((Action)(() => Task.Delay(1))); }", "BDT004")]
    [TestCase("void Test() { var step = Given(() => { _ = Task.Delay(1); }); }", "BDT004")]
    [TestCase("void Test() { var step = Given(() => { Task.Delay(1).ConfigureAwait(false); }); }", "BDT004")]
    [TestCase("ValueTask<int> Step() => default; void Test() { var step = Given(() => { Step(); }); }", "BDT004")]
    [TestCase("void Test() { var step = Given((Action)(async () => { await Task.Delay(1); })); }", "BDT004")]
    [TestCase("void Test() { var step = Given(() => { if (true) Task.Delay(1); Task.Delay(2); }); }", "BDT004")]
    public async Task Safe_fix_compiles_and_removes_diagnostic(string members, string id)
    {
        var (document, diagnostics) = await Document(members);
        using var workspace = document.Project.Solution.Workspace;
        var actions = await Fixes(document, diagnostics.First(d => d.Id == id));
        Assert.That(actions, Has.Count.EqualTo(1));
        var operations = await actions[0].GetOperationsAsync(default);
        var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var changed = solution.GetDocument(document.Id) ?? throw new InvalidOperationException("Missing fixed document");
        var compilation = await changed.Project.GetCompilationAsync() ?? throw new InvalidOperationException("Missing compilation");
        AnalyzerTests.AssertCompiles(compilation);
        Assert.That((await AnalyzerTests.Analyze(compilation)).Where(d => d.Id == id), Is.Empty,
            (await changed.GetTextAsync()).ToString());
        Assert.That((await changed.GetTextAsync()).ToString(), Does.Contain("await"));
    }

    [TestCase("void Test() { Given(() => {}).When(() => {}).Then(() => {}); }", "BDT001")]
    [TestCase("async Task Test() { Given(() => {}); await Task.Delay(1); }", "BDT001")]
    [TestCase("async void Step() { await Task.Delay(1); } void Test() { var step = Given(Step); }", "BDT004")]
    [TestCase("async void Test() { When(() => {}).Then(() => {}); await Task.Delay(1); }", "BDT001")]
    [TestCase("void Test() { var step = Given(() => { lock (this) { Task.Delay(1); } }); }", "BDT004")]
    [TestCase("Task Run(Action callback) => Task.CompletedTask; async Task Test() { Run(() => Given(() => {}).When(() => {}).Then(() => {})); }", "BDT001")]
    [TestCase("Task Run(Action callback) => Task.CompletedTask; async Task Test() { Run(() => Given(() => {}).When(() => {}).Then(() => {}).BDTestAsync()); }", "BDT001")]
    [TestCase("async void Step() { await Task.Delay(1); } void Test() { var step = Given(() => Step()); }", "BDT004")]
    [TestCase("async void Step() { await Task.Delay(1); } void Test() { var step = Given(() => { Step(); Task.Delay(1); }); }", "BDT004")]
    public async Task No_fix_when_signature_or_step_structure_needs_manual_change(string members, string id)
    {
        var (document, diagnostics) = await Document(members);
        using var workspace = document.Project.Solution.Workspace;
        var findings = diagnostics.Where(d => d.Id == id).ToArray();
        Assert.That(findings, Is.Not.Empty);
        foreach (var finding in findings)
            Assert.That(await Fixes(document, finding), Is.Empty);
    }

    [TestCase("BDT001")]
    [TestCase("BDT004")]
    public async Task Fixed_scenario_observes_asynchronous_step_completion(string id)
    {
        var testBody = id == "BDT001"
            ? "When((Func<Task>)Work).Then(AssertCompleted);"
            : "await Given((Action)(() => Work())).When(() => {}).Then(AssertCompleted).BDTestAsync();";
        var (document, diagnostics) = await Document("""
            bool completed;
            async Task Work() { await Task.Yield(); completed = true; }
            void AssertCompleted() { if (!completed) throw new Exception("Step was not awaited"); }
            public async Task<bool> Run() {
            """ + testBody + " return completed; }");
        using var workspace = document.Project.Solution.Workspace;
        var action = (await Fixes(document, diagnostics.Single(d => d.Id == id))).Single();
        var operations = await action.GetOperationsAsync(default);
        var changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)
            ?? throw new InvalidOperationException("Missing fixed document");
        var originalCompilation = await changed.Project.GetCompilationAsync()
            ?? throw new InvalidOperationException("Missing compilation");
        var compilation = originalCompilation.WithAssemblyName("FixedConsumer_" + Guid.NewGuid().ToString("N"));
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        Assert.That(emitted.Success, Is.True, string.Join(Environment.NewLine, emitted.Diagnostics));
        stream.Position = 0;
        var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
        var type = assembly.GetType("Consumer", throwOnError: true)!;
        using var run = BDTestRun.Start();
        var fixture = (BDTest.Test.BDTestBuilder)(Activator.CreateInstance(type, run) ?? throw new InvalidOperationException("Missing consumer instance"));
        var task = (Task<bool>)(type.GetMethod("Run")!.Invoke(fixture, null)
            ?? throw new InvalidOperationException("Missing consumer task"));
        Assert.That(await task, Is.True);
    }

    private static async Task<(Document Document, ImmutableArray<Diagnostic> Diagnostics)> Document(string members)
    {
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Consumer", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences(AnalyzerTests.References);
        var document = project.AddDocument("Consumer.cs", AnalyzerTests.Source(members));
        var compilation = await document.Project.GetCompilationAsync() ?? throw new InvalidOperationException("Missing compilation");
        AnalyzerTests.AssertCompiles(compilation);
        return (document, await AnalyzerTests.Analyze(compilation));
    }

    private static async Task<List<CodeAction>> Fixes(Document document, Diagnostic diagnostic)
    {
        var actions = new List<CodeAction>();
        await new BDTestCodeFixProvider().RegisterCodeFixesAsync(new CodeFixContext(document, diagnostic,
            (action, _) => actions.Add(action), default));
        return actions;
    }
}
