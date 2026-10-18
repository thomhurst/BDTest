using System.Collections.Concurrent;
using System.Security;
using BDTest.Attributes;
using BDTest.NUnit;
using BDTest.Test;
using NUnit.Framework;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace BDTest.Tests.Fixtures;

public class NUnitRunnerOutcomeTests
{
    [TestCase("plain-retry", TestStatus.Passed, Status.Passed, 1)]
    [TestCase("assert-retry", TestStatus.Passed, Status.Passed, 1)]
    [TestCase("multiple-retry", TestStatus.Passed, Status.Passed, 1)]
    [TestCase("async-multiple-retry", TestStatus.Passed, Status.Passed, 1)]
    [TestCase("plain-failure", TestStatus.Failed, Status.Failed, 1)]
    [TestCase("multiple-failure", TestStatus.Failed, Status.Failed, 1)]
    [TestCase("outside-before", TestStatus.Failed, Status.Passed, 0)]
    [TestCase("outside-after", TestStatus.Failed, Status.Passed, 0)]
    [TestCase("warning", TestStatus.Warning, Status.Passed, 0)]
    [TestCase("warning-retry", TestStatus.Warning, Status.Passed, 1)]
    [TestCase("caught-assert-retry", TestStatus.Passed, Status.Passed, 1)]
    [TestCase("caught-assert-failure", TestStatus.Failed, Status.Failed, 1)]
    [TestCase("outer-multiple-retry", TestStatus.Passed, Status.Passed, 1)]
    public void RunnerAndScenarioKeepTheirOwnOutcomes(string mode, TestStatus runnerStatus,
        Status scenarioStatus, int retryCount)
    {
        var name = $"{typeof(NUnitOutcomeProbe).FullName}.Run(\"{mode}\")";
        var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
        runner.Load(typeof(NUnitOutcomeProbe).Assembly, new Dictionary<string, object>
        {
            ["NumberOfTestWorkers"] = 0
        });
        var result = runner.Run(TestListener.NULL,
            TestFilter.FromXml($"<filter><test>{SecurityElement.Escape(name)}</test></filter>"));
        var testResult = Leaves(result).Single();
        var scenario = NUnitOutcomeProbe.Scenarios[mode];

        Assert.Multiple(() =>
        {
            Assert.That(testResult.FullName, Is.EqualTo(name));
            Assert.That(testResult.ResultState.Status, Is.EqualTo(runnerStatus), testResult.Message);
            Assert.That(scenario.Status, Is.EqualTo(scenarioStatus));
            Assert.That(scenario.RetryCount, Is.EqualTo(retryCount));
            if (mode.StartsWith("outside-"))
                Assert.That(testResult.Message, Does.Contain("outside scenario"));
            if (mode.StartsWith("warning"))
                Assert.That(testResult.AssertionResults.Any(assertion => assertion.Message.Contains("scenario warning")), Is.True);
        });
    }

    private static IEnumerable<ITestResult> Leaves(ITestResult result) => result.HasChildren
        ? result.Children.SelectMany(Leaves) : new[] { result };
}

[Explicit("Executed by NUnitRunnerOutcomeTests with an embedded NUnit runner.")]
[BDTestRetry(1)]
public class NUnitOutcomeProbe : NUnitBDTestBase<MyTestContext>
{
    internal static readonly ConcurrentDictionary<string, Scenario> Scenarios = new();
    private readonly BDTestRun _run = BDTestRun.Start();
    public override BDTestRun TestRun => _run;

    [TestCase("plain-retry")]
    [TestCase("assert-retry")]
    [TestCase("multiple-retry")]
    [TestCase("async-multiple-retry")]
    [TestCase("plain-failure")]
    [TestCase("multiple-failure")]
    [TestCase("outside-before")]
    [TestCase("outside-after")]
    [TestCase("warning")]
    [TestCase("warning-retry")]
    [TestCase("caught-assert-retry")]
    [TestCase("caught-assert-failure")]
    [TestCase("outer-multiple-retry")]
    public async Task Run(string mode)
    {
        using var outerScope = mode == "outer-multiple-retry" ? Assert.EnterMultipleScope() : null;
        var attempts = 0;
        if (mode == "outside-before")
        {
            try { Assert.Fail("outside scenario before"); }
            catch (AssertionException) { }
        }

        try
        {
            await When(async () =>
                {
                    await Task.Yield();
                    attempts++;
                    var fails = attempts == 1 || mode.EndsWith("-failure");
                    switch (mode)
                    {
                        case "plain-retry":
                        case "plain-failure":
                            if (fails) throw new InvalidOperationException("plain attempt failure");
                            break;
                        case "caught-assert-retry":
                        case "caught-assert-failure":
                            try { Assert.That(fails, Is.False, "caught attempt failure"); }
                            catch (AssertionException) { }
                            break;
                        case "warning-retry":
                            Assert.Warn("scenario warning");
                            if (fails) throw new InvalidOperationException("retry with warning");
                            break;
                        case "outer-multiple-retry":
                        case "assert-retry":
                            Assert.That(fails, Is.False, "assert attempt failure");
                            break;
                        case "multiple-retry":
                        case "multiple-failure":
                            Assert.Multiple(() => Assert.That(fails, Is.False, "multiple attempt failure"));
                            break;
                        case "async-multiple-retry":
                            await Assert.MultipleAsync(async () =>
                            {
                                await Task.Yield();
                                Assert.That(fails, Is.False, "async multiple attempt failure");
                            });
                            break;
                        case "warning":
                            Assert.Warn("scenario warning");
                            break;
                    }
                })
                .Then(() => { })
                .BDTestAsync();

            if (mode == "outside-after") Assert.Fail("outside scenario after");
        }
        finally
        {
            Scenarios[mode] = _run.Scenarios.Single();
        }
    }

    [OneTimeTearDown]
    public void DisposeRun() => _run.Dispose();
}
