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
    public async Task ExceptionAssertionsUseIsolatedRunsAndPreserveOtherRuns()
    {
        using var retainedFixture = new RetainedScenario();
        var retained = await retainedFixture.Run();
        using var nunitPass = new CustomPassExceptionsTests();
        await nunitPass.NUNitSuccessException();
        using var customPass = new CustomPassExceptionsTests();
        await customPass.CustomException();
        using var nunitIgnore = new IgnoreTests();
        await nunitIgnore.NUNitIgnoreException();
        using var customIgnore = new IgnoreTests();
        await customIgnore.CustomIgnoreException();
        foreach (var (exception, status) in new[]
                 {
                     (typeof(Exception), Status.Failed),
                     (typeof(NotImplementedException), Status.NotImplemented)
                 })
        {
            using var given = new ExceptionTests();
            await given.ThrowsExceptionOnGiven(exception, status);
            using var then = new ExceptionTests();
            await then.ThrowsExceptionOnThen(exception, status);
        }

        Assert.That(retainedFixture.TestRun.Scenarios, Does.Contain(retained));
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



    private sealed class RetainedScenario : RunTestBase
    {
        private readonly string _executionId = Guid.NewGuid().ToString();
        protected override string BDTestExecutionId => _executionId;

        public async Task<Scenario> Run() => await When(() => Console.WriteLine("retained scenario"))
            .Then(() => Console.WriteLine("unrelated to current assertions")).BDTestAsync();
    }
}
