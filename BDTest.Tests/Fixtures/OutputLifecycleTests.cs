using System.Collections;
using System.Reflection;
using BDTest.Maps;
using BDTest.Output;
using BDTest.Test;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class OutputLifecycleTests
{
    [Test]
    public void Startup_before_and_after_registration_is_delivered_once_in_order()
    {
        using var fixture = new Capture();
        TestOutputData.CollectStartupOutput(fixture.Id, "before", false, run: fixture.TestRun);
        var scenario = fixture.Register();
        TestOutputData.CollectStartupOutput(fixture.Id, "x", false, run: fixture.TestRun);
        TestOutputData.CollectStartupOutput(fixture.Id, "after", false, run: fixture.TestRun);

        Assert.That(scenario.TestStartupInformation,
            Is.EqualTo($"before{Environment.NewLine}xafter{Environment.NewLine}"));
        Assert.That(fixture.Register().TestStartupInformation, Is.Null.Or.Empty);
    }

    [Test]
    public async Task Startup_and_listeners_racing_registration_are_delivered_once()
    {
        for (var iteration = 0; iteration < 30; iteration++)
        {
            using var fixture = new Capture();
            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            var writers = Enumerable.Range(0, 4).Select(writer => Task.Run(async () =>
            {
                await start.Task;
                for (var index = 0; index < 50; index++)
                {
                    TestOutputData.CollectStartupOutput(fixture.Id, $"{writer}:{index}", false, run: fixture.TestRun);
                    fixture.TestRun.Store.ListenForScenario(fixture.Id, _ => Interlocked.Increment(ref calls));
                }
            })).ToArray();
            var registration = Task.Run(async () =>
            {
                await start.Task;
                return fixture.Register();
            });
            start.SetResult(true);
            await Task.WhenAll(writers);
            var scenario = await registration;

            var lines = Required(scenario.TestStartupInformation).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.That(lines, Has.Length.EqualTo(200));
            for (var writer = 0; writer < 4; writer++)
            {
                Assert.That(lines.Where(line => line.StartsWith($"{writer}:")),
                    Is.EqualTo(Enumerable.Range(0, 50).Select(index => $"{writer}:{index}")));
            }
            Assert.That(calls, Is.EqualTo(200));
        }
    }

    [Test]
    public void Status_controls_startup_capture_without_a_timestamp_heuristic()
    {
        using var fixture = new Capture();
        fixture.Select();
        var scenario = fixture.Register();
        var writer = new TestOutputData();
        TestOutputData.TestId = scenario.Guid;
        scenario.StartTime = DateTime.UtcNow;
        writer.Write("startup");
        Assert.That(scenario.TestStartupInformation, Is.EqualTo("startup"));

        scenario.Status = Status.Pending;
        scenario.StartTime = default;
        writer.Write("running");
        Assert.That(writer.ToString(), Is.EqualTo("running"));
        Assert.That(scenario.TestStartupInformation, Is.EqualTo("startup"));
    }

    [Test]
    public void Character_capture_and_explicit_teardown_preserve_newlines_and_json()
    {
        using var fixture = new Capture();
        fixture.Select();
        var writer = new TestOutputData();
        writer.Write("setup\r\n");
        var scenario = fixture.Register();
        writer.Write('x'); // Registered, but not started yet.
        scenario.StartTime = DateTime.Now;
        scenario.Status = Status.Pending;
        scenario.EndTime = DateTime.Now;
        TestOutputData.TestId = scenario.Guid;
        writer.Write("teardown\r\n");
        TestOutputData.CollectTearDownOutput(fixture.Id, "explicit", false, run: fixture.TestRun);
        writer.Write('!');

        Assert.That(scenario.TestStartupInformation, Is.EqualTo("setup\r\nx"));
        Assert.That(scenario.TearDownOutput, Is.EqualTo($"teardown\r\nexplicit{Environment.NewLine}!"));
        var restored = ReportJson.DeserializeObject<Scenario>(ReportJson.SerializeObject(scenario));
        Assert.That(restored.TestStartupInformation, Is.EqualTo(scenario.TestStartupInformation));
        Assert.That(restored.TearDownOutput, Is.EqualTo(scenario.TearDownOutput));
    }

    [Test]
    public void Framework_id_only_flow_captures_completed_scenario_teardown()
    {
        using var fixture = new Capture();
        fixture.Select();
        var scenario = fixture.Register();
        scenario.Status = Status.Passed;
        scenario.EndTime = DateTime.UtcNow;
        TestOutputData.TestId = null;
        var writer = new TestOutputData();

        writer.Write("teardown");
        writer.Write('!');

        Assert.That(scenario.TearDownOutput, Is.EqualTo("teardown!"));
        Assert.That(scenario.TestStartupInformation, Is.Null.Or.Empty);
    }

    [Test]
    public async Task Parallel_framework_ids_keep_setup_and_teardown_separate()
    {
        await Task.WhenAll(Enumerable.Range(0, 12).Select(index => Task.Run(() =>
        {
            using var fixture = new Capture();
            fixture.Select();
            var writer = new TestOutputData();
            var text = $"test-{index}\r\n";
            writer.Write(text);
            var scenario = fixture.Register();
            scenario.StartTime = DateTime.Now;
            scenario.Status = Status.Pending;
            scenario.EndTime = DateTime.Now;
            TestOutputData.TestId = scenario.Guid;
            writer.Write(text);
            Assert.That(scenario.TestStartupInformation, Is.EqualTo(text));
            Assert.That(scenario.TearDownOutput, Is.EqualTo(text));
        })));
    }

    [Test]
    public void Completion_without_scenario_discards_pending_output_and_listeners()
    {
        using var fixture = new Capture();
        using var other = new Capture();
        TestOutputData.CollectStartupOutput(fixture.Id, "discard", false, run: fixture.TestRun);
        TestOutputData.CollectStartupOutput(other.Id, "keep", false, run: other.TestRun);
        var calls = 0;
        fixture.TestRun.Store.ListenForScenario(fixture.Id, _ => calls++);

        fixture.Complete();

        // Check retained roots as well as observable delivery: an empty listener entry also leaks IDs.
        foreach (var field in typeof(BDTest.Maps.TestRunStore).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            if (field.Name is "ScenarioListeners" or "PendingStartupOutput" &&
                field.GetValue(fixture.TestRun.Store) is IDictionary dictionary)
            {
                Assert.That(dictionary.Contains(fixture.Id), Is.False, field.Name);
            }
        }
        Assert.That(fixture.Register().TestStartupInformation, Is.Null.Or.Empty);
        Assert.That(calls, Is.Zero);
        Assert.That(other.Register().TestStartupInformation, Is.EqualTo($"keep{Environment.NewLine}"));
    }

    [Test]
    public async Task Concurrent_explicit_teardown_keeps_each_line_intact()
    {
        using var fixture = new Capture();
        var scenario = fixture.Register();
        await Task.WhenAll(Enumerable.Range(0, 4).Select(writer => Task.Run(() =>
        {
            for (var index = 0; index < 100; index++)
            {
                TestOutputData.CollectTearDownOutput(fixture.Id, $"{writer}:{index}", false, run: fixture.TestRun);
            }
        })));
        fixture.Complete();

        var lines = Required(scenario.TearDownOutput).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines, Has.Length.EqualTo(400));
        for (var writer = 0; writer < 4; writer++)
        {
            Assert.That(lines.Where(line => line.StartsWith($"{writer}:")),
                Is.EqualTo(Enumerable.Range(0, 100).Select(index => $"{writer}:{index}")));
        }
    }

    [TestCase("")]
    [TestCase("x")]
    [TestCase("line")]
    public void Public_output_methods_keep_existing_newline_rules(string text)
    {
        using var fixture = new Capture();
        var scenario = fixture.Register();
        fixture.WriteStartupOutput(text);
        fixture.WriteTearDownOutput(text);

        Assert.That(scenario.TestStartupInformation,
            Is.EqualTo(text + (text.Length > 1 ? Environment.NewLine : string.Empty)));
        Assert.That(scenario.TearDownOutput, Is.EqualTo(text + Environment.NewLine));
    }

    [Test]
    public void Character_capture_allocation_grows_linearly()
    {
        MeasureAllocations(100); // Warm up capture and registration paths.
        var small = MeasureAllocations(2000);
        var large = MeasureAllocations(8000);
        Assert.That(large, Is.LessThan(small * 6), $"2,000 chars: {small:N0} bytes; 8,000 chars: {large:N0} bytes");
        Assert.That(large, Is.LessThan(512_000), "Capture should not allocate callbacks or growing strings per character.");
        TestContext.WriteLine($"Setup + teardown allocations: 2,000 chars = {small:N0} B; 8,000 chars = {large:N0} B");
    }

    private static long MeasureAllocations(int count)
    {
        using var fixture = new Capture();
        fixture.Select();
        var writer = new TestOutputData();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < count; i++) writer.Write('s');
        var scenario = fixture.Register();
        TestOutputData.TestId = scenario.Guid;
        scenario.StartTime = DateTime.Now;
        scenario.Status = Status.Pending;
        scenario.EndTime = DateTime.Now;
        for (var i = 0; i < count; i++) writer.Write('t');
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.That(scenario.TestStartupInformation, Is.EqualTo(new string('s', count)));
        Assert.That(scenario.TearDownOutput, Is.EqualTo(new string('t', count)));
        return allocated;
    }

    private sealed class Capture : RunTestBase, IDisposable
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        private readonly List<Scenario> _scenarios = new();
        protected override string BDTestExecutionId => Id;
        public void Select()
        {
            TestOutputData.Run = TestRun;
            TestOutputData.TestId = null;
            TestOutputData.FrameworkExecutionId = Id;
        }
        public Scenario Register()
        {
            var scenario = new Scenario(new List<Step>(), When(() => NoOp()).Definition);
            _scenarios.Add(scenario);
            return scenario;
        }
        public void Complete() => BDTest.Tests.Helpers.LegacyLifecycle.Complete(this);
        public override void Dispose()
        {
            Complete();
            TestRun.Store.ScenariosByTestFrameworkId.TryRemove(Id, out _);
            foreach (var scenario in _scenarios) TestRun.Store.ScenariosByInternalId.TryRemove(Required(scenario.Guid), out _);
            base.Dispose();
        }
        private static void NoOp() { }
    }
}
