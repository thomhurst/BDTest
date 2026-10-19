using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using BDTest;
using BDTest.Metadata;
using BDTest.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace BDTest.SourceGenerator.Tests;

public class StepMetadataGeneratorTests
{
    [Test]
    public void All_step_names_resolve_private_overloaded_and_generic_methods()
    {
        var consumer = Compile("""
            public sealed class Consumer : BDTest.Probe
            {
                private int Value = 3;
                [BDTest.StepText("number {value}", "value=override")]
                [SkipStepIf("private overload")]
                private void Step(int value) { }
                private void Step(string value) { }
                private void Generic<T>(T value) { }
                public static object[] Run()
                {
                    var test = new Consumer();
                    test.Given(() => test.Step(1));
                    return test.RunSteps();
                }
                private object[] RunSteps()
                {
                    Captures.Clear();
                    Given(() => Step(Value))
                        .When(() => Generic<int>(Value))
                        .Then(() => Step("text"))
                        .And(() => Step(4))
                        .But(() => Step(5));
                    Value = 19;
                    return Captures.ToArray();
                }
            }
            public sealed class SkipStepIfAttribute : BDTest.SkipStepAttribute
            {
                public SkipStepIfAttribute(string reason) { Reason = reason; }
                public string Reason { get; }
            }
            """);
        var sites = Run(consumer);
        Assert.That(sites, Has.Length.EqualTo(5));
        var methods = sites.Select(s => (s.Metadata.Method ?? throw new AssertionException("Expected step method metadata.")).Resolve(consumer)).ToArray();
        Assert.That(methods[0].IsPrivate, Is.True);
        Assert.That(methods[0].GetParameters()[0].ParameterType, Is.EqualTo(typeof(int)));
        Assert.That(methods[1].GetGenericArguments(), Is.EqualTo(new[] { typeof(int) }));
        Assert.That(methods[2].GetParameters()[0].ParameterType, Is.EqualTo(typeof(string)));
        var text = methods[0].GetCustomAttribute<StepTextAttribute>() ?? throw new InvalidOperationException("Missing step text attribute");
        Assert.That(text.Text, Is.EqualTo("number {value}"));
        Assert.That(text.ParameterOverrides, Is.EqualTo(new[] { "value=override" }));
        var skip = methods[0].GetCustomAttributes<SkipStepAttribute>().Single();
        Assert.That(skip.GetType().GetProperty("Reason")!.GetValue(skip), Is.EqualTo("private overload"));
        Assert.That(Read(sites[0], consumer), Is.EqualTo(new object[] { 19 }));
        Assert.That(Read(sites[1], consumer), Is.EqualTo(new object[] { 19 }));
    }

    [Test]
    public void Boxing_member_values_and_constants_retains_runtime_values()
    {
        var consumer = Compile("""
            public sealed class Consumer : BDTest.Probe
            {
                private int Value = 19;
                private void Step(object value) { }
                public static object[] Run() { var test = new Consumer(); return test.Build(); }
                private object[] Build()
                {
                    Given(() => Step(Value)).When(() => Step("ready")).Then(() => Step((byte)7));
                    return Captures.ToArray();
                }
            }
            """);
        var sites = Run(consumer);
        Assert.That(Read(sites[0], consumer), Is.EqualTo(new object[] { 19 }));
        Assert.That(Read(sites[1], consumer), Is.EqualTo(new object[] { "ready" }));
        Assert.That(Read(sites[2], consumer).Single(), Is.TypeOf<byte>().And.EqualTo((byte)7));
    }

    [Test]
    public void Forwarded_runner_token_does_not_disable_other_automatic_arguments()
    {
        var consumer = Compile("""
            public sealed class Consumer : BDTest.Probe
            {
                private int Value = 19;
                private System.Threading.Tasks.Task Step(int value, System.Threading.CancellationToken token)
                    => System.Threading.Tasks.Task.CompletedTask;
                public static object[] Run() { var test = new Consumer(); return test.Build(); }
                private object[] Build()
                {
                    Given(token => Step(Value, token));
                    return Captures.ToArray();
                }
            }
            """);
        var site = Run(consumer).Single();
        Assert.That(site.Metadata.SupportsArgumentCapture, Is.True);
        Assert.That(Read(site, consumer), Is.EqualTo(new object?[] { 19, null }));
    }

    [Test]
    public void Binding_does_not_evaluate_getters_and_accessors_read_current_state()
    {
        var consumer = Compile("""
            public sealed class Consumer : BDTest.Probe
            {
                public static int Reads;
                public int Value;
                private int Current { get { Reads++; return Value; } }
                private void Step(int value) { throw new System.Exception("must not run"); }
                public static object[] Run()
                {
                    var test = new Consumer();
                    test.Build();
                    return test.Captures.ToArray();
                }
                private void Build() { Given(() => Step(Current)); }
            }
            """);
        var site = Run(consumer).Single();
        Assert.That(site.Metadata.TryBindArguments(consumer, site.Step, out var accessors), Is.True);
        var type = consumer.GetType("Consumer", throwOnError: true)!;
        Assert.That(type.GetField("Reads")!.GetValue(null), Is.EqualTo(0));
        type.GetField("Value")!.SetValue(site.Step.Target, 42);
        Assert.That(accessors[0](), Is.EqualTo(42));
        type.GetField("Value")!.SetValue(site.Step.Target, 57);
        Assert.That(accessors[0](), Is.EqualTo(57));
        Assert.That(type.GetField("Reads")!.GetValue(null), Is.EqualTo(2));
    }

    [Test]
    public void Open_generic_declaring_types_preserve_caller_text_fallback()
    {
        var consumer = Compile("""
            public sealed class Consumer : BDTest.Probe
            {
                private sealed class Hidden<T>
                {
                    private static void Step<TMethod>(T first, TMethod second) { }
                    public static object[] Build()
                    {
                        var probe = new BDTest.Probe();
                        probe.Given(() => Step<string>(second: "second", first: default(T)));
                        return probe.Captures.ToArray();
                    }
                }
                public static object[] Run() { return Hidden<int>.Build(); }
            }
            """);
        // The call occurs in an open generic declaration: no closed type identity is available there.
        var site = Run(consumer).Single();
        Assert.That(site.Metadata.Method, Is.Null);
        Assert.That(site.Metadata.ArgumentExpression, Does.Contain("Step<string>"));
    }

    [Test]
    public void Constructed_generic_receivers_resolve_without_inaccessible_references()
    {
        var consumer = Compile("""
            public sealed class Consumer : BDTest.Probe
            {
                private sealed class Hidden<T> { public void Step(T value) { } }
                private Hidden<int> Target = new Hidden<int>();
                public static object[] Run() { var test = new Consumer(); return test.Build(); }
                private object[] Build() { Given(() => Target.Step(2)); return Captures.ToArray(); }
            }
            """);
        var site = Run(consumer).Single();
        var method = (site.Metadata.Method ?? throw new AssertionException("Expected step method metadata.")).Resolve(consumer);
        Assert.That(method.DeclaringType!.IsConstructedGenericType, Is.True);
        Assert.That(method.GetParameters()[0].ParameterType, Is.EqualTo(typeof(int)));
        Assert.That(Read(site, consumer), Is.EqualTo(new object[] { 2 }));
    }

    [TestCase("Given(() => { Step(Value); });", false)]
    [TestCase("Given(() => Step(GetValue()));", true)]
    [TestCase("int local = 9; Given(() => Step(local));", true)]
    [TestCase("Given(() => Step());", true)]
    [TestCase("Given(() => Params(1, 2));", true)]
    [TestCase("Given(() => Step((int?)Value ?? 0));", true)]
    public void Unsupported_arguments_keep_identity_when_possible_and_caller_text(string body, bool hasMethod)
    {
        var consumer = Compile("""
            public sealed class Consumer : BDTest.Probe
            {
                private int Value;
                private int GetValue() { throw new System.Exception("must not run"); }
                private void Step(int value = 1) { }
                private void Params(params int[] values) { }
                public static object[] Run() { var test = new Consumer(); return test.Build(); }
                private object[] Build() { BODY return Captures.ToArray(); }
            }
            """.Replace("BODY", body));
        var site = Run(consumer).Single();
        Assert.That(site.Metadata.Method != null, Is.EqualTo(hasMethod));
        Assert.That(site.Metadata.ArgumentExpression, Does.StartWith("() =>"));
        Assert.That(site.Metadata.TryBindArguments(consumer, site.Step, out var accessors), Is.False);
        Assert.That(accessors, Is.Empty);
    }

    [Test]
    public void Closure_target_is_rejected_without_inspecting_compiler_generated_fields()
    {
        var consumer = Compile("""
            public sealed class Consumer : BDTest.Probe
            {
                private int Value = 5;
                private void Step(int value) { }
                public static object[] Run() { var test = new Consumer(); return test.Build(); }
                private object[] Build()
                {
                    int local = 0;
                    System.Action other = () => { local++; Value++; };
                    Given(() => Step(Value));
                    System.GC.KeepAlive(other);
                    return Captures.ToArray();
                }
            }
            """);
        var site = Run(consumer).Single();
        // Roslyn may use a display class shared by the two lambdas. Never guess its layout.
        if (site.Step.Target!.GetType().Name.Contains("DisplayClass"))
            Assert.That(site.Metadata.TryBindArguments(consumer, site.Step, out _), Is.False);
        else
            Assert.That(Read(site, consumer), Is.EqualTo(new object[] { 5 }));
        var capturedLocal = new object();
        Action closure = () => GC.KeepAlive(capturedLocal);
        Assert.That(site.Metadata.TryBindArguments(consumer, closure, out _), Is.False);
    }

    [Test]
    public void Static_member_paths_named_arguments_and_null_constants_remain_lazy()
    {
        var consumer = Compile("""
            public sealed class Consumer : BDTest.Probe
            {
                public static int Reads;
                public static Consumer Current { get { Reads++; return Instance; } }
                public static Consumer Instance = new Consumer();
                private int Value = 21;
                private static void Step(string first, int second) { }
                public static object[] Run()
                {
                    var test = new Consumer();
                    test.Given(() => Step(second: Current.Value, first: null));
                    return test.Captures.ToArray();
                }
            }
            """);
        var site = Run(consumer).Single();
        Assert.That(site.Metadata.TryBindArguments(consumer, site.Step, out var accessors), Is.True);
        Assert.That(consumer.GetType("Consumer", throwOnError: true)!.GetField("Reads")!.GetValue(null), Is.EqualTo(0));
        Assert.That(accessors.Select(a => a()).ToArray(), Is.EqualTo(new object?[] { null, 21 }));
    }

    [TestCase("")]
    [TestCase("namespace BDTest;")]
    public void Unrelated_method_names_do_not_generate_metadata(string namespaceDeclaration)
    {
        var compilation = CreateCompilation(namespaceDeclaration + "class Unrelated { void Given(System.Action step) { } void Test() { Given(() => { }); } }");
        var driver = RunGenerator(compilation, out _);
        Assert.That(driver.GetRunResult().GeneratedTrees.Any(t => t.FilePath.EndsWith("BDTest.StepMetadata.g.cs")), Is.False);
    }

    [Test]
    public void Missing_generator_uses_null_catalog_fallback()
    {
        Assert.That(StepMetadataCatalog.Find(typeof(StepMetadataGeneratorTests).Assembly, "test.cs", 1, "() => Step()"), Is.Null);
    }

    [TestCase(LanguageVersion.CSharp10)]
    [TestCase(LanguageVersion.CSharp11)]
    [TestCase(LanguageVersion.CSharp12)]
    public void Generated_source_compiles_on_supported_language_versions(LanguageVersion languageVersion)
    {
        var compilation = CreateCompilation(Probe + "\n" + """
            public sealed class Consumer : BDTest.Probe
            {
                private void Step(int value) { }
                private void Build() { Given(() => Step(1)); }
            }
            """, languageVersion);
        RunGenerator(compilation, out var generated);
        Assert.That(generated.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error), Is.Empty);
    }

    [Test]
    public void Loaded_catalog_lookup_finds_new_assemblies_and_rejects_ambiguous_identities()
    {
        var path = "unique-" + Guid.NewGuid().ToString("N") + ".cs";
        var source = "#line 600 \"" + path + "\"\n" + """
            public sealed class Consumer : BDTest.Probe { private void Build() { Given(() => Step(1)); } private void Step(int value) { } }
            """;
        Assert.That(StepMetadataCatalog.Find(path, 600, "() => Step(1)", out _), Is.Null);
        var first = Compile(source);
        Assert.That(StepMetadataCatalog.Find(path, 600, "() => Step(1)", out var owner), Is.Not.Null);
        Assert.That(owner, Is.SameAs(first));
        Compile(source);
        Assert.That(StepMetadataCatalog.Find(path, 600, "() => Step(1)", out owner), Is.Null);
        Assert.That(owner, Is.Null);
    }

    [TestCase("byte")]
    [TestCase("sbyte")]
    [TestCase("short")]
    [TestCase("ushort")]
    public void Narrow_integral_constants_preserve_the_parameter_type(string parameterType)
    {
        var consumer = Compile("""
            public sealed class Consumer : BDTest.Probe
            {
                private void Step(TYPE value) { }
                public static object[] Run()
                {
                    var test = new Consumer();
                    test.Given(() => test.Step(1));
                    return test.Captures.ToArray();
                }
            }
            """.Replace("TYPE", parameterType));
        var site = Run(consumer).Single();
        var expectedType = (site.Metadata.Method ?? throw new AssertionException("Expected step method metadata.")).Resolve(consumer).GetParameters()[0].ParameterType;
        var argument = Read(site, consumer).Single() ?? throw new InvalidOperationException("Missing numeric argument");
        Assert.That(argument.GetType(), Is.EqualTo(expectedType));
        Assert.That(Convert.ToInt32(argument), Is.EqualTo(1));
    }

    [TestCase("1")]
    [TestCase("null")]
    public void Nullable_intermediate_paths_use_caller_text_fallback(string initialValue)
    {
        var consumer = Compile("""
            public sealed class Consumer : BDTest.Probe
            {
                private int? Optional = INITIAL;
                private void Step(int value) { }
                public static object[] Run() { return new Consumer().Build(); }
                private object[] Build()
                {
                    Given(() => Step(Optional.Value));
                    return Captures.ToArray();
                }
            }
            """.Replace("INITIAL", initialValue));
        var site = Run(consumer).Single();
        Assert.That((site.Metadata.Method ?? throw new AssertionException("Expected step method metadata.")).Resolve(consumer).Name, Is.EqualTo("Step"));
        Assert.That(site.Metadata.ArgumentExpression, Does.Contain("Optional.Value"));
        Assert.That(site.Metadata.TryBindArguments(consumer, site.Step, out var accessors), Is.False);
        Assert.That(accessors, Is.Empty);
    }

    [Test]
    public void Dynamic_parameter_signatures_resolve_as_object()
    {
        var consumer = Compile("""
            public sealed class Consumer : BDTest.Probe
            {
                private void Step(dynamic value) { }
                private void Generic<T>(T value) { }
                public static object[] Run()
                {
                    var test = new Consumer();
                    test.Given(() => test.Step(1)).Then(() => test.Generic<dynamic>(1));
                    return test.Captures.ToArray();
                }
            }
            """);
        foreach (var site in Run(consumer))
        {
            Assert.That((site.Metadata.Method ?? throw new AssertionException("Expected step method metadata.")).Resolve(consumer).GetParameters()[0].ParameterType, Is.EqualTo(typeof(object)));
        }
    }

    [Test]
    public void Base_virtual_property_reads_use_caller_text_fallback()
    {
        var consumer = Compile("""
            public class Parent : BDTest.Probe
            {
                protected virtual int Value => 1;
            }
            public sealed class Consumer : Parent
            {
                protected override int Value => 2;
                private void Step(int value) { }
                public static object[] Run() { return new Consumer().Build(); }
                private object[] Build()
                {
                    Given(() => Step(base.Value));
                    return Captures.ToArray();
                }
            }
            """);
        var site = Run(consumer).Single();
        Assert.That((site.Metadata.Method ?? throw new AssertionException("Expected step method metadata.")).Resolve(consumer).Name, Is.EqualTo("Step"));
        Assert.That(site.Metadata.ArgumentExpression, Does.Contain("base.Value"));
        Assert.That(site.Metadata.TryBindArguments(consumer, site.Step, out var accessors), Is.False);
        Assert.That(accessors, Is.Empty);
    }

    private static object?[] Read(Captured site, Assembly consumer)
    {
        Assert.That(site.Metadata.TryBindArguments(consumer, site.Step, out var accessors), Is.True);
        return accessors.Select(a => a()).ToArray();
    }

    private static Captured[] Run(Assembly assembly)
    {
        var values = (object[])(assembly.GetType("Consumer", throwOnError: true)!.GetMethod("Run")!.Invoke(null, null) ?? throw new InvalidOperationException("Missing captures"));
        return values.Cast<object[]>().Select(value => new Captured((StepCallSiteMetadata)value[0], (Delegate)value[1])).ToArray();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Path_mapped_caller_information_finds_method_metadata(bool lineDirective)
    {
        var root = Path.GetFullPath("mapped-source") + Path.DirectorySeparatorChar;
        var resolver = new SourceFileResolver(ImmutableArray<string>.Empty, Directory.GetCurrentDirectory(),
            ImmutableArray.Create(new KeyValuePair<string, string>(root, "/_/")));
        var consumer = Compile((lineDirective ? "#line 600 \"generated/consumer.cs\"\n" : "") + """
            public sealed class Consumer : BDTest.Probe
            {
                private void Step(int value) { }
                public static object[] Run()
                {
                    var test = new Consumer();
                    test.Given(() => test.Step(42));
                    return test.Captures.ToArray();
                }
            }
            """, resolver, Path.Combine(root, "consumer.cs"));
        var site = Run(consumer).Single();
        Assert.That((site.Metadata.Method ?? throw new AssertionException("Expected step method metadata.")).Resolve(consumer).Name, Is.EqualTo("Step"));
        Assert.That(Read(site, consumer), Is.EqualTo(new object[] { 42 }));
    }

    [TestCase("{value}", null, 0)]
    [TestCase("{value:N2}", null, 0)]
    [TestCase("{value}", "value={missing}", 0)]
    [TestCase("{value}", "missing=text", 1)]
    [TestCase("{value}", "0:legacy", 1)]
    [TestCase("{missing}", null, 1)]
    [TestCase("{0}", null, 1)]
    [TestCase("{0,10:N2}", null, 1)]
    [TestCase("{0,garbage}", null, 0)]
    [TestCase("{0, -10:N2}", null, 1)]
    [TestCase("{token}", null, 1)]
    [TestCase("{{unknown}} {{{value}}}", null, 0)]
    [TestCase("{ not a placeholder } and unmatched {", null, 0)]
    [TestCase("{\"name\": 1}", null, 0)]
    public void Step_templates_are_validated_without_executing_user_code(string template, string? parameterOverride, int count)
    {
        var attribute = "[BDTest.StepText(" + Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(template, true) +
            (parameterOverride == null ? "" : ", " + Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(parameterOverride, true)) + ")]";
        var compilation = CreateCompilation("class Consumer { " + attribute +
            " void Step(decimal value, System.Threading.CancellationToken token) { throw new System.Exception(); } }");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new StepMetadataGenerator().AsSourceGenerator() },
            parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.First().Options);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(Enumerable.Repeat("BDT002", count)),
            string.Join(Environment.NewLine, diagnostics));
        Assert.That(diagnostics.All(d => d.Location.SourceSpan == compilation.SyntaxTrees.Single().GetRoot()
            .DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.AttributeSyntax>().Single().Span), Is.True);
    }

    private static Assembly Compile(string consumer, SourceReferenceResolver? resolver = null, string sourcePath = "consumer.cs")
    {
        var compilation = CreateCompilation(Probe + "\n" + consumer, sourcePath: sourcePath);
        if (resolver != null)
            compilation = compilation.WithOptions(compilation.Options.WithSourceReferenceResolver(resolver));
        RunGenerator(compilation, out var generated);
        using var stream = new MemoryStream();
        var result = generated.Emit(stream);
        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Diagnostics));
        stream.Position = 0;
        return AssemblyLoadContext.Default.LoadFromStream(stream);
    }

    private static GeneratorDriver RunGenerator(CSharpCompilation compilation, out Compilation generated)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new StepMetadataGenerator().AsSourceGenerator() },
            parseOptions: (CSharpParseOptions)compilation.SyntaxTrees.First().Options);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out generated, out var diagnostics);
        Assert.That(diagnostics, Is.Empty);
        return driver;
    }

    private static CSharpCompilation CreateCompilation(string source, LanguageVersion languageVersion = LanguageVersion.CSharp12,
        string sourcePath = "consumer.cs")
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? throw new InvalidOperationException("Missing runtime assembly paths")).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p)).ToList();
        if (!references.Any(r => r.FilePath == typeof(StepCallSiteMetadata).Assembly.Location))
            references.Add(MetadataReference.CreateFromFile(typeof(StepCallSiteMetadata).Assembly.Location));
        return CSharpCompilation.Create("Consumer_" + Guid.NewGuid().ToString("N"),
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(languageVersion), path: sourcePath) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private sealed record Captured(StepCallSiteMetadata Metadata, Delegate Step);

    private const string Probe = """
        namespace BDTest
        {
            public class Probe : BDTestBase
            {
                // This probe inspects generated call sites without executing a scenario.
                public override BDTestRun TestRun => throw new System.InvalidOperationException("Probe does not execute tests");
                public System.Collections.Generic.List<object> Captures = new System.Collections.Generic.List<object>();
                private Probe Capture(System.Delegate step, string expression, string file, int line)
                {
                    var metadata = BDTest.Metadata.StepMetadataCatalog.Find(GetType().Assembly, file, line, expression);
                    if (metadata == null) throw new System.Exception("Missing call site: " + file + ":" + line + " " + expression);
                    Captures.Add(new object[] { metadata, step });
                    return this;
                }
                public Probe Given(System.Action step, [System.Runtime.CompilerServices.CallerArgumentExpression("step")] string expression = null,
                    [System.Runtime.CompilerServices.CallerFilePath] string file = null, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
                    { return Capture(step, expression, file, line); }
                public Probe Given(System.Func<System.Threading.CancellationToken, System.Threading.Tasks.Task> step,
                    [System.Runtime.CompilerServices.CallerArgumentExpression("step")] string expression = null,
                    [System.Runtime.CompilerServices.CallerFilePath] string file = null, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
                    { return Capture(step, expression, file, line); }
                public Probe When(System.Action step, [System.Runtime.CompilerServices.CallerArgumentExpression("step")] string expression = null,
                    [System.Runtime.CompilerServices.CallerFilePath] string file = null, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
                    { return Capture(step, expression, file, line); }
                public Probe Then(System.Action step, [System.Runtime.CompilerServices.CallerArgumentExpression("step")] string expression = null,
                    [System.Runtime.CompilerServices.CallerFilePath] string file = null, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
                    { return Capture(step, expression, file, line); }
                public Probe And(System.Action step, [System.Runtime.CompilerServices.CallerArgumentExpression("step")] string expression = null,
                    [System.Runtime.CompilerServices.CallerFilePath] string file = null, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
                    { return Capture(step, expression, file, line); }
                public Probe But(System.Action step, [System.Runtime.CompilerServices.CallerArgumentExpression("step")] string expression = null,
                    [System.Runtime.CompilerServices.CallerFilePath] string file = null, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
                    { return Capture(step, expression, file, line); }
            }
        }
        """;
}
