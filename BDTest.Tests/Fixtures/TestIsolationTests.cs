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
    public void ExceptionAssertionsIgnoreRetainedScenariosAndRestoreRegistrations()
    {
        NUnitBDTestBase<MyTestContext>.SetupNUnitExceptions();
        var successTypes = BDTestSettings.CustomExceptionSettings.SuccessExceptionTypes.ToArray();
        var inconclusiveTypes = BDTestSettings.CustomExceptionSettings.InconclusiveExceptionTypes.ToArray();
        var retained = new RetainedScenario().Run();

        new CustomPassExceptionsTests().NUNitSuccessException();
        new CustomPassExceptionsTests().CustomException();
        new IgnoreTests().NUNitIgnoreException();
        new IgnoreTests().CustomIgnoreException();
        foreach (var (exception, status) in new[]
                 {
                     (typeof(Exception), Status.Failed),
                     (typeof(NotImplementedException), Status.NotImplemented)
                 })
        {
            new ExceptionTests().ThrowsExceptionOnGiven(exception, status);
            new ExceptionTests().ThrowsExceptionOnThen(exception, status);
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
    public void FixtureRestoresPreviousSettings(string environment, string tag)
    {
        var originalEnvironment = BDTestSettings.Environment;
        var originalTag = BDTestSettings.Tag;
        try
        {
            BDTestSettings.Environment = environment;
            BDTestSettings.Tag = tag;
            var fixture = new SetupTeardownFixture();
            fixture.Settings();
            Assert.That(BDTestSettings.Environment, Is.EqualTo("Local"));
            Assert.That(BDTestSettings.Tag, Is.EqualTo("AcceptanceTests"));
            fixture.RestoreSettings();
            Assert.That(BDTestSettings.Environment, Is.EqualTo(environment));
            Assert.That(BDTestSettings.Tag, Is.EqualTo(tag));
        }
        finally
        {
            BDTestSettings.Environment = originalEnvironment;
            BDTestSettings.Tag = originalTag;
        }
    }

    [Test, Repeat(3)]
    public void ExampleReportsUseOwnedOutputAndCleanupPreservesOtherDirectories()
    {
        var foreignFolder = Path.Combine(Path.GetTempPath(), "BDTest-foreign-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(foreignFolder);
        var stale = Path.Combine(foreignFolder, "stale.json");
        try
        {
            File.WriteAllText(stale, "stale output from another run");
            var folder = FileHelpers.GetUniqueTestOutputFolder();
            Assert.That(folder, Does.StartWith(FileHelpers.OutputRoot + Path.DirectorySeparatorChar));
            Assert.That(Directory.GetFiles(folder), Is.Empty);
            Example.HtmlReportGenerationExample.GenerateReports(folder);
            var json = FileHelpers.GetUniqueTestJsonFilePath();
            Assert.That(File.ReadAllText(json), Does.Contain("Scenarios"));
            Assert.That(Directory.GetFiles(folder, "*.html"), Has.Length.EqualTo(1));

            FileHelpers.DeleteOutputRoot();
            Assert.That(Directory.Exists(FileHelpers.OutputRoot), Is.False);
            Assert.That(File.ReadAllText(stale), Is.EqualTo("stale output from another run"));
        }
        finally
        {
            FileHelpers.DeleteOutputRoot();
            Directory.Delete(foreignFolder, recursive: true);
        }
    }

    private sealed class RetainedScenario : BDTestBase
    {
        private readonly string _executionId = Guid.NewGuid().ToString();
        protected override string BDTestExecutionId => _executionId;

        public Scenario Run() => When(() => Console.WriteLine("retained scenario"))
            .Then(() => Console.WriteLine("unrelated to current assertions")).BDTest();
    }
}
