using System.Reflection;
using System.Reflection.Emit;
using BDTest.Attributes;
using BDTest.Interfaces;
using BDTest.Helpers;
using BDTest.Test;
#if BDTEST_NUNIT
using AssemblyOwner = BDTest.NUnit.NUnitBDTestAssembly;
using ScenarioFactory = BDTest.NUnit.NUnitBDTestScenario;
using Adapter = BDTest.NUnit.NUnitBDTestBase<object>;
using TestAttribute = global::NUnit.Framework.TestAttribute;
#elif BDTEST_MSTEST
using AssemblyOwner = BDTest.MSTest.MSTestBDTestAssembly;
using ScenarioFactory = BDTest.MSTest.MSTestBDTestScenario;
using Adapter = BDTest.MSTest.MSTestBDTestBase<object>;
using TestAttribute = Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
#elif BDTEST_TUNIT
using AssemblyOwner = BDTest.TUnit.TUnitBDTestAssembly;
using ScenarioFactory = BDTest.TUnit.TUnitBDTestScenario;
using Adapter = BDTest.TUnit.TUnitBDTestBase<object>;
using TestAttribute = global::TUnit.Core.TestAttribute;
#elif BDTEST_XUNIT
using AssemblyOwner = BDTest.xUnit.XUnitBDTestAssembly;
using ScenarioFactory = BDTest.xUnit.XUnitBDTestScenario;
using Adapter = BDTest.xUnit.XUnitBDTestBase<object>;
using TestAttribute = Xunit.FactAttribute;
#endif

namespace BDTest.Adapters.Conformance;

#if BDTEST_MSTEST
[global::BDTest.MSTest.BDTestClass]
#endif
public class AssemblyRunContract : Adapter
{
    [Test]
    public void AdapterUsesActualFixtureAssembly() =>
        Require(ReferenceEquals(TestRun, AssemblyOwner.GetRun(GetType().Assembly)), "adapter selected another assembly's run");

    [Test]
    public void ConfigurationCannotReenterAndFailedInitializationCanRetry()
    {
        var assembly = CreateAssembly(typeof(ReentrantConfiguration));
        ReentrantConfiguration.Assembly = assembly;
        try
        {
            try
            {
                AssemblyOwner.GetRun(assembly);
                throw new Exception("recursive configuration was accepted");
            }
            catch (InvalidOperationException exception)
            {
                Require(exception.Message.Contains("configuration"), "unexpected initialization failure");
            }
            ReentrantConfiguration.Assembly = null;
            var run = AssemblyOwner.GetRun(assembly);
            Require(run.Scenarios.Count == 0, "failed configuration left partial state");
        }
        finally
        {
            ReentrantConfiguration.Assembly = null;
            AssemblyOwner.Dispose(assembly);
        }
    }

    [Test]
    public async Task AssembliesKeepFrozenOptionsReportsAndDisposalSeparate()
    {
        var firstAssembly = CreateAssembly(typeof(FirstConfiguration));
        var secondAssembly = CreateAssembly(typeof(SecondConfiguration));
        try
        {
            var lookups = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
                Task.Run(() => AssemblyOwner.GetRun(firstAssembly))));
            var first = lookups[0];
            Require(lookups.All(run => ReferenceEquals(first, run)), "concurrent initialization created multiple runs");
            Require(FirstConfiguration.Calls == 1, "configuration ran more than once");
            var second = AssemblyOwner.GetRun(secondAssembly);
            Require(first.ReportId != second.ReportId, "assembly reports share an ID");
            Require(first.Scenarios.Count == 0 && second.Scenarios.Count == 0, "new assembly store was not empty");
            CheckFrameworkOutcomes(first);
            CheckFrameworkOutcomes(second);

            // Mutating the retained options after initialization must not affect the frozen run.
            FirstConfiguration.Options!.SuccessExceptionTypes.Clear();
            FirstConfiguration.Options.SkipTestRules.Add(_ => true);
            FirstConfiguration.Options.StepHooks.Clear();
            var outcomes = await Task.WhenAll(Execute(first, "first assembly"), Execute(second, "second assembly"));
            Require(outcomes[0].Status == Status.Passed, "first assembly lost its frozen success exception");
            Require(outcomes[1].Status == Status.Failed, "second assembly inherited the first exception registration");
            Require(FirstConfiguration.Hook.BeforeCount == 3, "first assembly lost its frozen hook");
            Require(SecondConfiguration.Hook.BeforeCount == 3, "second assembly hook was not isolated");
            Require(outcomes[0].Steps![0].StepText!.Contains("first conversion"), $"first assembly lost its typed converter: {outcomes[0].Steps![0].StepText}");
            Require(outcomes[1].Steps![0].StepText!.Contains("second conversion"), "second assembly inherited another converter");
            Require(outcomes[0].Steps![1].Status == Status.Skipped, "first assembly lost its skip rule");
            Require(outcomes[1].Steps![1].Status == Status.Passed, "second assembly inherited another skip rule");
            Require(ReferenceEquals(first.Scenarios.Single(), outcomes[0]), "first store contains another assembly's scenario");
            Require(ReferenceEquals(second.Scenarios.Single(), outcomes[1]), "second store contains another assembly's scenario");
            var firstHtml = BDTestHtmlHelper.GetTestHtmlReport(new BDTestRunDescriptor(), first);
            var secondHtml = BDTestHtmlHelper.GetTestHtmlReport(new BDTestRunDescriptor(), second);
            Require(firstHtml.Contains("first assembly") && !firstHtml.Contains("second assembly"), "first report leaked another assembly's output");
            Require(secondHtml.Contains("second assembly") && !secondHtml.Contains("first assembly"), "second report leaked another assembly's output");

            AssemblyOwner.Dispose(firstAssembly);
            AssemblyOwner.Dispose(firstAssembly);
            Require(first.Scenarios.Count == 0, "assembly disposal did not clear scenarios");
            Require(second.Scenarios.Count == 1, "disposing first assembly cleared the second");
            try
            {
                BDTestScenario.For<object>(this, first);
                throw new InvalidOperationException("disposed assembly run remained usable");
            }
            catch (ObjectDisposedException) { }

            var restarted = AssemblyOwner.GetRun(firstAssembly);
            Require(restarted.ReportId != first.ReportId && restarted.Scenarios.Count == 0, "reused host did not start fresh");
            Require(FirstConfiguration.Calls == 2, "reset did not rerun assembly configuration");
            Require(ReferenceEquals(second, AssemblyOwner.GetRun(secondAssembly)), "reset changed the other assembly run");
        }
        finally
        {
            AssemblyOwner.Dispose(firstAssembly);
            AssemblyOwner.Dispose(secondAssembly);
            FirstConfiguration.Calls = 0;
        }
    }

    private async Task<Scenario> Execute(BDTestRun run, string output)
    {
        var host = BDTestScenario.For<object>(this, run);
        host.BeginTest("shared assembly-local test ID");
        host.WriteStartupOutput(output);
        var argument = new AssemblyArgument();
        try
        {
            await host.Given(() => ReadArgument(argument)).WithArgs(() => argument).And(OptionalStep)
                .When(ThrowConfiguredException).Then(() => GC.KeepAlive(output)).BDTestAsync();
        }
        catch (ConfiguredSuccessException) { }
        finally { await host.EndTestAsync(); }
        return run.Scenarios.Single();
    }

    private static void ThrowConfiguredException() => throw new ConfiguredSuccessException();

    [StepText("assembly argument {0}")]
    private static void ReadArgument(AssemblyArgument argument) => GC.KeepAlive(argument);

    [AssemblySkip]
    private static void OptionalStep() { }

    private sealed class AssemblySkipAttribute : SkipStepAttribute;
    public sealed class AssemblyArgument;
    private sealed class AssemblyConverter(string text) : IStepTextStringConverter<AssemblyArgument>
    {
        string IStepTextStringConverter<AssemblyArgument>.ConvertToString(AssemblyArgument argument) => text;
    }

    private static Assembly CreateAssembly(Type configuration)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("BDTest.AssemblyProbe." + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.RunAndCollect);
        assembly.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(BDTestConfigurationAttribute).GetConstructor([typeof(Type)])!, [configuration]));
        return assembly;
    }

    private static void CheckFrameworkOutcomes(BDTestRun run)
    {
        // Inspect the snapshot without throwing native skip exceptions into the surrounding runner.
        var options = typeof(BDTestRun).GetProperty("Options", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(run)!;
        var frameworkTypes = new[] { "SuccessExceptionTypes", "SkipExceptionTypes", "InconclusiveExceptionTypes" }
            .SelectMany(name => (IEnumerable<Type>)options.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(options)!)
            .Where(type => type != typeof(ConfiguredSuccessException)).ToArray();
#if BDTEST_NUNIT
        Type[] expected = [typeof(global::NUnit.Framework.SuccessException), typeof(global::NUnit.Framework.IgnoreException), typeof(global::NUnit.Framework.InconclusiveException)];
#elif BDTEST_MSTEST
        Type[] expected = [typeof(Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException)];
#elif BDTEST_TUNIT
        Type[] expected = [typeof(global::TUnit.Core.Exceptions.SkipTestException), typeof(global::TUnit.Core.Exceptions.InconclusiveTestException)];
#elif BDTEST_XUNIT
        Type[] expected = [typeof(Xunit.Sdk.SkipException)];
#endif
        Require(frameworkTypes.Length == expected.Length && expected.All(frameworkTypes.Contains), "assembly registered another framework's outcomes or duplicates");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public sealed class ConfiguredSuccessException : Exception;

    public sealed class ReentrantConfiguration : IBDTestConfiguration
    {
        public static Assembly? Assembly;
        public void Configure(BDTestOptions options)
        {
            if (Assembly != null) AssemblyOwner.GetRun(Assembly);
        }
    }

    public sealed class FirstConfiguration : IBDTestConfiguration
    {
        public static int Calls;
        public static BDTestOptions? Options;
        public static ProbeHook Hook = new();
        public void Configure(BDTestOptions options)
        {
            Calls++;
            Options = options;
            Hook = new ProbeHook();
            options.SuccessExceptionTypes.Add(typeof(ConfiguredSuccessException));
            options.StepHooks.Add(Hook);
            options.SkipStepRules.Add<AssemblySkipAttribute>(_ => true);
            options.RegisterCustomStringConverter<AssemblyArgument>(new AssemblyConverter("first conversion"));
        }
    }

    public sealed class SecondConfiguration : IBDTestConfiguration
    {
        public static ProbeHook Hook = new();
        public void Configure(BDTestOptions options)
        {
            Hook = new ProbeHook();
            options.StepHooks.Add(Hook);
            options.RegisterCustomStringConverter<AssemblyArgument>(new AssemblyConverter("second conversion"));
        }
    }

    public sealed class ProbeHook : IStepHook
    {
        public int BeforeCount;
        public Task BeforeStepAsync(Step step) { Interlocked.Increment(ref BeforeCount); return Task.CompletedTask; }
        public Task AfterStepAsync(Step step) => Task.CompletedTask;
    }
}

// Exercise the overload without a supplied run through each runner's native lifecycle.
// This class deliberately does not inherit an adapter base or call BeginTest/EndTest.
#if BDTEST_NUNIT
[global::BDTest.NUnit.BDTestFixture]
public class AssemblyCompositionContract : global::BDTest.NUnit.INUnitBDTest
#elif BDTEST_MSTEST
[global::BDTest.MSTest.BDTestClass]
public class AssemblyCompositionContract : global::BDTest.MSTest.IBDTestMSTestTest
#elif BDTEST_TUNIT
public class AssemblyCompositionContract : global::BDTest.TUnit.IBDTestTUnitTest
#else
public class AssemblyCompositionContract
#endif
{
    private readonly BDTestScenario<object> _host;

    public AssemblyCompositionContract() => _host = ScenarioFactory.For<object>(this);

    public BDTestBase BDTestHost => _host;

#if BDTEST_MSTEST
    public Microsoft.VisualStudio.TestTools.UnitTesting.TestContext TestContext
    {
        get;
        set
        {
            field = value;
            global::BDTest.MSTest.MSTestBDTestLifecycle.Initialize(this, value);
        }
    } = null!;
#endif

    [Test, ScenarioText("Assembly factory native lifecycle")]
    public async Task FactorySelectsAssemblyRunAndExecutesWithNativeMetadata()
    {
        var run = AssemblyOwner.GetRun(GetType().Assembly);
        if (!ReferenceEquals(_host.TestRun, run))
            throw new InvalidOperationException("Factory did not select the fixture assembly run.");

        var scenario = await _host.When(() => GC.KeepAlive(_host.Context))
            .Then(() => GC.KeepAlive(run)).BDTestAsync();

        if (scenario.Status != Status.Passed || scenario.ReportId != run.ReportId ||
            !run.Scenarios.Contains(scenario) ||
            scenario.GetScenarioText() != "Assembly factory native lifecycle")
            throw new InvalidOperationException("Native lifecycle did not record the scenario and method metadata in the assembly run.");
#if BDTEST_NUNIT
        var nativeContext = global::NUnit.Framework.Internal.TestExecutionContext.CurrentContext;
        var expectedId = $"{nativeContext.CurrentTest.Id}_{nativeContext.CurrentRepeatCount}";
#elif BDTEST_XUNIT
        var expectedId = Xunit.TestContext.Current.Test!.UniqueID;
#elif BDTEST_TUNIT
        var expectedId = global::TUnit.Core.TestContext.Current!.Id;
#else
        var expectedPrefix = $"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestDisplayName ?? TestContext.TestName}_";
        if (scenario.FrameworkTestId?.StartsWith(expectedPrefix, StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Native lifecycle did not bind the MSTest invocation.");
#endif
#if !BDTEST_MSTEST
        if (scenario.FrameworkTestId != expectedId)
            throw new InvalidOperationException("Native lifecycle did not bind the runner execution ID.");
#endif
    }
}
