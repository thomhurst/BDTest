using System.IO;
using System.Collections.Concurrent;
using BDTest.TUnit;
using BDTest.Settings;
using BDTest.Test;
using BDTest.Tests.Helpers;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class TestIsolationTests
{
    [Test, Repeat(2)]
    public async Task ExceptionAssertionsUseIsolatedRunsAndPreserveOtherRuns()
    {
        using var retainedFixture = new RetainedScenario();
        var retained = await retainedFixture.Run();
        using var customPass = new CustomPassExceptionsTests();
        await customPass.CustomException();
        using var tunitSkip = new IgnoreTests();
        await tunitSkip.TUnitSkipException();
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

        await Assert.That(retainedFixture.TestRun.Scenarios).Contains(retained);
    }



    [Test, Repeat(2)]
    public async Task JsonLookupRejectsAmbiguousOutput()
    {
        var folder = FileHelpers.GetUniqueTestOutputFolder();
        var stale = Path.Combine(folder, "stale.json");
        var current = Path.Combine(folder, "current.json");
        try
        {
            File.WriteAllText(stale, "stale");
            File.WriteAllText(current, "current");
            await Assert.That(() => FileHelpers.GetUniqueTestJsonFilePath()).ThrowsExactly<InvalidOperationException>();
            File.Delete(stale);
            await Assert.That(FileHelpers.GetUniqueTestJsonFilePath()).IsEqualTo(current);
            File.Delete(current);
            await Assert.That(() => FileHelpers.GetUniqueTestJsonFilePath()).ThrowsExactly<InvalidOperationException>();
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

        public async Task<BDTest.Contracts.ScenarioResult> Run() => await When(() => Console.WriteLine("retained scenario"))
            .Then(() => Console.WriteLine("unrelated to current assertions")).BDTestAsync();
    }
}
