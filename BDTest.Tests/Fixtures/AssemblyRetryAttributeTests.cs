using System.Reflection;
using System.Reflection.Emit;
using BDTest.Attributes;
using BDTest.Test;
using BDTest.Tests.Helpers;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class AssemblyRetryAttributeTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AssemblyAttributeRetriesUntilSuccess(bool suspend)
    {
        using var fixture = CreateScenario(typeof(RetryScenario), 2);
        fixture.Failures = 2;

        var scenario = await fixture.Run(suspend);

        await Assert.That(scenario.Status).IsEqualTo(Status.Passed);
        await Assert.That(scenario.RetryCount).IsEqualTo(2);
        await Assert.That(fixture.Executions).IsEqualTo(3);
    }

    [Test]
    [Arguments(null, 0)]
    [Arguments(0, 0)]
    [Arguments(2, 2)]
    public async Task AssemblyAttributeLimitsRetries(int? assemblyRetryCount, int expectedRetries)
    {
        using var fixture = CreateScenario(typeof(RetryScenario), assemblyRetryCount);
        fixture.Failures = 3;

        await Assert.That(async () => { await fixture.Run(true); }).ThrowsExactly<InvalidOperationException>();

        await Assert.That(BDTestUtil.GetScenarios(fixture.TestRun).Single().RetryCount).IsEqualTo(expectedRetries);
        await Assert.That(fixture.Executions).IsEqualTo(expectedRetries + 1);
    }

    [Test]
    [Arguments(typeof(ClassRetryScenario), 1)]
    [Arguments(typeof(MethodRetryScenario), 0)]
    public async Task MoreSpecificAttributesOverrideAssembly(Type fixtureType, int expectedRetries)
    {
        using var fixture = CreateScenario(fixtureType, 2);
        fixture.Failures = 3;

        await Assert.That(async () => { await fixture.Run(true); }).ThrowsExactly<InvalidOperationException>();

        await Assert.That(BDTestUtil.GetScenarios(fixture.TestRun).Single().RetryCount).IsEqualTo(expectedRetries);
        await Assert.That(fixture.Executions).IsEqualTo(expectedRetries + 1);
    }

    [Test]
    [Arguments(typeof(RetryScenario), 2)]
    [Arguments(typeof(ClassRetryScenario), 1)]
    [Arguments(typeof(MethodRetryScenario), 0)]
    public async Task AttributesReplaceLargerRunDefault(Type fixtureType, int expectedRetries)
    {
        var predicateCalls = 0;
        using var fixture = CreateScenario(fixtureType, 2, options =>
            options.RetryTestRules.Add(_ => { predicateCalls++; return true; }, 5));
        fixture.Failures = 10;

        await Assert.That(async () => { await fixture.Run(true); }).ThrowsExactly<InvalidOperationException>();

        await Assert.That(fixture.Executions).IsEqualTo(expectedRetries + 1);
        await Assert.That(predicateCalls).IsZero();
        await Assert.That(BDTestUtil.GetScenarios(fixture.TestRun).Single().Attempts).Count().IsEqualTo(expectedRetries + 1);
    }

    private static RetryScenario CreateScenario(Type baseType, int? retryCount, Action<BDTestOptions>? configure = null)
    {
        // Keep assembly defaults isolated from the rest of the test suite.
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"RetryTests_{Guid.NewGuid():N}"), AssemblyBuilderAccess.RunAndCollect);
        if (retryCount.HasValue)
        {
            assembly.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(BDTestRetryAttribute).GetConstructor(new[] { typeof(int) })!,
                new object[] { retryCount.Value }));
        }

        var type = assembly.DefineDynamicModule("Tests")
            .DefineType("Scenario", TypeAttributes.Public, baseType).CreateType()!;
        var fixture = (RetryScenario)Activator.CreateInstance(type)!;
        configure?.Invoke(fixture.Options);
        fixture.Start();
        return fixture;
    }

    public class RetryScenario : RunTestBase
    {
        public void Start() => BeginTest(Guid.NewGuid().ToString("N"), GetType().GetMethod(nameof(Run)));
        public int Failures { get; set; }
        public int Executions { get; private set; }

        public virtual async Task<BDTest.Contracts.ScenarioResult> Run(bool suspend)
        {
            if (suspend)
                await Task.Yield();

            return await Given(() => Execute(suspend)).When(() => NoOp()).Then(() => NoOp()).BDTestAsync();
        }

        private async Task Execute(bool suspend)
        {
            if (suspend)
                await Task.Yield();

            if (++Executions <= Failures)
                throw new InvalidOperationException("Retry this scenario.");
        }

        private static void NoOp() { }
    }

    [BDTestRetry(1)]
    public class ClassRetryScenario : RetryScenario
    {
    }

    public class MethodRetryScenario : ClassRetryScenario
    {
        [BDTestRetry(0)]
        public override Task<BDTest.Contracts.ScenarioResult> Run(bool suspend) => base.Run(suspend);
    }
}
