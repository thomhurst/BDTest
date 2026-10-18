using System.Reflection;
using System.Reflection.Emit;
using BDTest.Attributes;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class AssemblyRetryAttributeTests
{
    [SetUp]
    public void Reset() => TestResetHelper.ResetData();

    [TestCase(false)]
    [TestCase(true)]
    public async Task AssemblyAttributeRetriesUntilSuccess(bool suspend)
    {
        var fixture = CreateScenario(typeof(RetryScenario), 2);
        fixture.Failures = 2;

        var scenario = await fixture.Run(suspend);

        Assert.That(scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(scenario.RetryCount, Is.EqualTo(2));
        Assert.That(fixture.Executions, Is.EqualTo(3));
    }

    [TestCase(null, 0)]
    [TestCase(0, 0)]
    [TestCase(2, 2)]
    public async Task AssemblyAttributeLimitsRetries(int? assemblyRetryCount, int expectedRetries)
    {
        var fixture = CreateScenario(typeof(RetryScenario), assemblyRetryCount);
        fixture.Failures = 3;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Run(true));

        Assert.That(BDTestUtil.GetScenarios().Single().RetryCount, Is.EqualTo(expectedRetries));
        Assert.That(fixture.Executions, Is.EqualTo(expectedRetries + 1));
    }

    [TestCase(typeof(ClassRetryScenario), 1)]
    [TestCase(typeof(MethodRetryScenario), 0)]
    public async Task MoreSpecificAttributesOverrideAssembly(Type fixtureType, int expectedRetries)
    {
        var fixture = CreateScenario(fixtureType, 2);
        fixture.Failures = 3;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Run(true));

        Assert.That(BDTestUtil.GetScenarios().Single().RetryCount, Is.EqualTo(expectedRetries));
        Assert.That(fixture.Executions, Is.EqualTo(expectedRetries + 1));
    }

    private static RetryScenario CreateScenario(Type baseType, int? retryCount)
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
        return (RetryScenario)Activator.CreateInstance(type)!;
    }

    public class RetryScenario : BDTestBase
    {
        public int Failures { get; set; }
        public int Executions { get; private set; }

        public virtual async Task<Scenario> Run(bool suspend)
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
        public override Task<Scenario> Run(bool suspend) => base.Run(suspend);
    }
}
