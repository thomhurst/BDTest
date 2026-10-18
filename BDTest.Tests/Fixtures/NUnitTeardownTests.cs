using System.Reflection;
using BDTest.Attributes;
using BDTest.NUnit;
using BDTest.Output;
using BDTest.Test;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace BDTest.Tests.Fixtures;

public class NUnitTeardownHookTests
{
    [Test]
    public void AdapterDeclaresOneTeardownHook()
    {
        var hooks = typeof(NUnitBDTestBase<MyTestContext>)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(method => method.IsDefined(typeof(TearDownAttribute), inherit: false))
            .Select(method => method.Name);

        Assert.That(hooks, Is.EqualTo(new[] { "MarkTestAsComplete" }));
    }
}

[TestFixture(false)]
[TestFixture(true)]
public class NUnitTeardownTests : NUnitBDTestBase<MyTestContext>
{
    private readonly BDTestRun _run;
    private readonly List<string> _events = new();
    private MyTestContext _scenarioContext;
    private Scenario _scenario;
    private int _attempts;
    private int _amendments;
    private int _completionCalls;
    private bool _completing;
    private bool _observing;
    private bool _retryCorrected;

    public NUnitTeardownTests(bool explicitRun)
    {
        if (explicitRun) _run = BDTestRun.Start();
    }

    public override BDTestRun TestRun => _run ?? base.TestRun;

    protected override string BDTestExecutionId
    {
        get
        {
            var id = base.BDTestExecutionId;
            // Each lifecycle operation reads the ID. Observe state between those operations.
            if (_completing && !_observing)
            {
                _observing = true;
                try
                {
                    if (!_retryCorrected && TestContext.CurrentContext.Result.Outcome == ResultState.Success)
                    {
                        _retryCorrected = true;
                        _events.Add("retry corrected");
                    }
                    _ = Context;
                }
                finally
                {
                    _observing = false;
                }
            }
            return id;
        }
    }

    [Test, BDTestRetry(1)]
    public async Task RetriedScenarioPassesBeforeContextAndOutputAreCleared()
    {
        ContextAmendment = _ =>
        {
            _amendments++;
            if (_completing) _events.Add("context removed");
        };
        _scenario = await Given(() => RememberContext())
            .When(() => FailFirstAttempt())
            .Then(() => RememberContext())
            .BDTestAsync();

        Assert.That(_scenario.Status, Is.EqualTo(Status.Passed));
        Assert.That(_scenario.RetryCount, Is.EqualTo(1));
        Assert.That(TestContext.CurrentContext.Result.Outcome, Is.EqualTo(ResultState.Failure),
            "The failed NUnit assertion needs correction even though the scenario passed on retry.");
        _events.Add("test finished");
    }

    // Deliberately no attribute: NUnit must still invoke the user's virtual override.
    protected override void MarkTestAsComplete()
    {
        _completionCalls++;
        _events.Add(ReferenceEquals(Context, _scenarioContext) ? "context present" : "context already removed");
        _events.Add(TestOutputData.FrameworkExecutionId == BDTestExecutionId ? "output active" : "output already cleared");
        WriteTearDownOutput("completion override output");
        _completing = true;
        base.MarkTestAsComplete();
        _events.Add(TestOutputData.FrameworkExecutionId == null && TestOutputData.TestId == null
            ? "test completed" : "test not completed");
        _ = Context;
        _completing = false;
    }

    [OneTimeTearDown]
    public void VerifyTeardownSequence()
    {
        try
        {
            Assert.That(_events, Is.EqualTo(new[]
            {
                "test finished", "context present", "output active", "retry corrected", "context removed", "test completed"
            }));
            Assert.That(_completionCalls, Is.EqualTo(1));
            Assert.That(_amendments, Is.EqualTo(3), "Two attempt contexts and one replacement after context removal.");
            Assert.That(_scenario.TearDownOutput, Does.Contain("completion override output"));
        }
        finally
        {
            _run?.Dispose();
        }
    }

    private void RememberContext() => _scenarioContext = Context;

    private void FailFirstAttempt() => Assert.That(++_attempts, Is.EqualTo(2));
}
