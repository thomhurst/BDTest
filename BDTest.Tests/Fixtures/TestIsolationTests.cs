using System.Collections.Concurrent;
using BDTest.NUnit;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Tests.Helpers;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class TestIsolationTests
{
    [Test, Repeat(3)]
    public async Task ExceptionAssertionsIgnoreRetainedScenariosAndRestoreRegistrations()
    {
        LegacyNUnitBDTestBase<MyTestContext>.RegisterLegacyOutcomes();
        var successTypes = BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes.ToArray();
        var inconclusiveTypes = BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.ToArray();
        var retained = await new RetainedScenario().Run();

        await new CustomPassExceptionsTests().NUNitSuccessException();
        await new CustomPassExceptionsTests().CustomException();
        await new IgnoreTests().NUNitIgnoreException();
        await new IgnoreTests().CustomIgnoreException();
        foreach (var (exception, status) in new[]
                 {
                     (typeof(Exception), Status.Failed),
                     (typeof(NotImplementedException), Status.NotImplemented)
                 })
        {
            await new ExceptionTests().ThrowsExceptionOnGiven(exception, status);
            await new ExceptionTests().ThrowsExceptionOnThen(exception, status);
        }

        Assert.That(BDTestUtil.GetScenarios(), Does.Contain(retained));
        Assert.That(BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes, Is.EquivalentTo(successTypes));
        Assert.That(BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes, Is.EquivalentTo(inconclusiveTypes));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExceptionRegistrationPreservesExistingEntries(bool alreadyRegistered)
    {
        var types = new ConcurrentBag<Type>(new[] { typeof(ArgumentException), typeof(ArgumentException) });
        if (alreadyRegistered)
        {
            types.Add(typeof(MyAllowedException));
        }

        var original = types.ToArray();
        var registration = new ExceptionRegistration(types, typeof(MyAllowedException));
        Assert.That(types, Does.Contain(typeof(MyAllowedException)));
        // Another owner's registration added after ours must survive cleanup too.
        types.Add(typeof(InvalidOperationException));
        registration.Dispose();
        registration.Dispose();
        Assert.That(types, Is.EquivalentTo(original.Append(typeof(InvalidOperationException))));
    }

    [Test, Repeat(3)]
    public void JsonLookupRejectsAmbiguousOutput()
    {
        var folder = FileHelpers.GetUniqueTestOutputFolder();
        var stale = Path.Combine(folder, "stale.json");
        var current = Path.Combine(folder, "current.json");
        try
        {
            File.WriteAllText(stale, "stale");
            File.WriteAllText(current, "current");
            Assert.Throws<InvalidOperationException>(() => FileHelpers.GetUniqueTestJsonFilePath());
            File.Delete(stale);
            Assert.That(FileHelpers.GetUniqueTestJsonFilePath(), Is.EqualTo(current));
            File.Delete(current);
            Assert.Throws<InvalidOperationException>(() => FileHelpers.GetUniqueTestJsonFilePath());
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [TestCase(null, null)]
    [TestCase("original environment", "original tag")]
    public void FixtureRestoresPreviousSettings(string? environment, string? tag)
    {
        var originalEnvironment = LegacySettings.Get<string>("Environment");
        var originalTag = LegacySettings.Get<string>("Tag");
        try
        {
            LegacySettings.Set("Environment", environment);
            LegacySettings.Set("Tag", tag);
            var fixture = new SetupTeardownFixture();
            fixture.Settings();
            Assert.That(LegacySettings.Get<string>("Environment"), Is.EqualTo("Local"));
            Assert.That(LegacySettings.Get<string>("Tag"), Is.EqualTo("AcceptanceTests"));
            fixture.RestoreSettings();
            Assert.That(LegacySettings.Get<string>("Environment"), Is.EqualTo(environment));
            Assert.That(LegacySettings.Get<string>("Tag"), Is.EqualTo(tag));
        }
        finally
        {
            LegacySettings.Set("Environment", originalEnvironment);
            LegacySettings.Set("Tag", originalTag);
        }
    }

    private sealed class RetainedScenario : BDTestBase
    {
        private readonly string _executionId = Guid.NewGuid().ToString();
        protected override string BDTestExecutionId => _executionId;

        public async Task<Scenario> Run() => await When(() => Console.WriteLine("retained scenario"))
            .Then(() => Console.WriteLine("unrelated to current assertions")).BDTestAsync();
    }
}
