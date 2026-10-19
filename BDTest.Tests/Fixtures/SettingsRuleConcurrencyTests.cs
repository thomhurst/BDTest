using BDTest.Attributes;
using BDTest.Settings.Retry;
using BDTest.Settings.Skip;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class SettingsRuleConcurrencyTests
{
    [Test]
    [Arguments("retry")]
    [Arguments("skip-step")]
    [Arguments("skip-test")]
    public async Task RegistrationDuringEvaluationUsesAStableSnapshot(string kind)
    {
        var (add, read) = CreateRules(kind);
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        var calls = new List<int>();
        add(() =>
        {
            calls.Add(1);
            entered.Set();
            if (!resume.Wait(TimeSpan.FromSeconds(20)))
            {
                throw new TimeoutException("Registration did not release the predicate.");
            }
            return false;
        });
        add(() => { calls.Add(2); return false; });

        var evaluation = Task.Run(() => read().Any(condition => condition()));
        try
        {
            await Assert.That(entered.Wait(TimeSpan.FromSeconds(20))).IsTrue();
            // Registration must finish while a user predicate is still running.
            await Task.Run(() => add(() => { calls.Add(3); return true; }))
                .WaitAsync(TimeSpan.FromSeconds(20));
        }
        finally
        {
            resume.Set();
            await evaluation.WaitAsync(TimeSpan.FromSeconds(20));
        }

        await Assert.That(await evaluation).IsFalse();
        await Assert.That(calls).IsEquivalentTo(new[] { 1, 2 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        calls.Clear();
        await Assert.That(read().Any(condition => condition())).IsTrue();
        await Assert.That(calls).IsEquivalentTo(new[] { 1, 2, 3 }, global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("retry")]
    [Arguments("skip-step")]
    [Arguments("skip-test")]
    public async Task PredicateCanRegisterARuleForTheNextEvaluation(string kind)
    {
        var (add, read) = CreateRules(kind);
        var registered = false;
        add(() =>
        {
            if (!registered)
            {
                registered = true;
                add(() => true);
            }
            return false;
        });

        await Assert.That(read().Any(condition => condition())).IsFalse();
        await Assert.That(read().Any(condition => condition())).IsTrue();
    }

    [Test]
    [Arguments("retry")]
    [Arguments("skip-step")]
    [Arguments("skip-test")]
    public async Task ConcurrentRegistrationsRetainEveryRule(string kind)
    {
        var (add, read) = CreateRules(kind);
        var calls = new List<int>();
        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        const int writerCount = 8;
        const int rulesPerWriter = 64;
        var writers = Enumerable.Range(0, writerCount).Select(writer => Task.Run(async () =>
        {
            await start.Task;
            for (var index = 0; index < rulesPerWriter; index++)
            {
                var id = writer * rulesPerWriter + index;
                add(() => { calls.Add(id); return false; });
            }
        })).ToArray();

        start.SetResult(true);
        await Task.WhenAll(writers).WaitAsync(TimeSpan.FromSeconds(20));
        await Assert.That(read().Any(condition => condition())).IsFalse();

        await Assert.That(calls).IsEquivalentTo(Enumerable.Range(0, writerCount * rulesPerWriter), global::TUnit.Assertions.Enums.CollectionOrdering.Any);
        for (var writer = 0; writer < writerCount; writer++)
        {
            await Assert.That(calls.Where(id => id / rulesPerWriter == writer)).IsEquivalentTo(Enumerable.Range(writer * rulesPerWriter, rulesPerWriter), global::TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    private static (Action<Func<bool>> Add, Func<IEnumerable<Func<bool>>> Read) CreateRules(string kind)
    {
        switch (kind)
        {
            case "retry":
                var retryRules = new RetryTestRules();
                var exception = new Exception();
                return (condition => retryRules.Add(_ => condition(), 1),
                    () => retryRules.Rules.Select(rule => new Func<bool>(() => rule.Condition(exception))));
            case "skip-step":
                var stepRules = new SkipStepRules();
                var attribute = new TestSkipAttribute();
                return (condition => stepRules.Add<TestSkipAttribute>(_ => condition()),
                    () => stepRules.Rules.Select(rule => new Func<bool>(() => rule.Condition(attribute))));
            case "skip-test":
                var testRules = new SkipTestRules();
                return (condition => testRules.Add(_ => condition()),
                    () => testRules.Rules.Select(rule => new Func<bool>(() => rule(null!))));
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    private sealed class TestSkipAttribute : SkipStepAttribute;
}
