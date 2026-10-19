using BDTest.Maps;
using BDTest.Output;
using BDTest.Test;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

[NotInParallel]
public class BulkConsoleOutputTests
{
    [Test]
    [Arguments("string")]
    [Arguments("array")]
    [Arguments("slice")]
    [Arguments("line")]
    [Arguments("array-line")]
    [Arguments("slice-line")]
    [Arguments("character-line")]
    [Arguments("empty-line")]
    public async Task Bulk_writes_preserve_text_and_forward_without_character_calls(string form)
    {
        using var capture = new Capture();
        var expected = WriteForm(capture.Writer, form);

        await Assert.That(capture.Writer.ToString()).IsEqualTo(expected);
        await Assert.That(capture.Original.ToString()).IsEqualTo(expected);
        await Assert.That(capture.Original.CharacterWrites).IsZero();
        await Assert.That(capture.Original.BulkWrites).IsEqualTo(1);
    }

    [Test]
    [Arguments("string")]
    [Arguments("array")]
    [Arguments("slice")]
    [Arguments("line")]
    [Arguments("array-line")]
    [Arguments("slice-line")]
    [Arguments("character-line")]
    [Arguments("empty-line")]
    public async Task Capture_writer_accepts_bulk_writes_directly(string form)
    {
        using var capture = new Capture();
        capture.Writer.Two.NewLine = "<capture-nl>";
        var expected = WriteForm(capture.Writer.Two, form);
        await Assert.That(capture.Writer.ToString()).IsEqualTo(expected);
        await Assert.That(capture.Original.ToString()).IsEmpty();
    }

    [Test]
    public async Task Character_null_empty_and_mixed_writes_preserve_order()
    {
        using var capture = new Capture();
        capture.Writer.Write('a');
        capture.Writer.Write((string?)null);
        capture.Writer.Write((char[]?)null);
        capture.Writer.Write("");
        capture.Writer.Write(Array.Empty<char>(), 0, 0);
        capture.Writer.Write("bc");
        capture.Writer.Write(new[] { 'x', 'd', 'e', 'x' }, 1, 2);
        capture.Writer.WriteLine((string?)null);
        capture.Writer.WriteLine((char[]?)null);
        capture.Writer.WriteLine("f");
        var expected = "abcde<nl><nl>f<nl>";
        await Assert.That(capture.Writer.ToString()).IsEqualTo(expected);
        await Assert.That(capture.Original.ToString()).IsEqualTo(expected);
        await Assert.That(capture.Original.CharacterWrites).IsEqualTo(1);
    }

    [Test]
    public async Task Invalid_slices_do_not_capture_or_forward_any_text()
    {
        using var capture = new Capture();
        await Assert.That(() => capture.Writer.Write(null!, 0, 0)).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => capture.Writer.Write(new char[2], -1, 1)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => capture.Writer.Write(new char[2], 0, -1)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => capture.Writer.Write(new char[2], 1, 2)).Throws<ArgumentException>();
        await Assert.That(() => capture.Writer.WriteLine(new char[2], 1, 2)).Throws<ArgumentException>();
        await Assert.That(capture.Writer.ToString()).IsEmpty();
        await Assert.That(capture.Original.ToString()).IsEmpty();
    }

    [Test]
    public async Task Startup_and_teardown_capture_raw_chunks_without_extra_newlines()
    {
        using var capture = new Capture(registerScenario: false);
        capture.Writer.Write("startup");
        capture.Writer.Write(new[] { '-', 's', 'l', 'i', 'c', 'e', '-' }, 1, 5);
        capture.Writer.WriteLine("line");
        capture.RegisterScenario();
        await Assert.That(capture.Scenario.TestStartupInformation).IsEqualTo("startupsliceline<nl>");

        capture.Scenario.EndTime = DateTime.UtcNow;
        capture.Writer.Write("teardown");
        capture.Writer.WriteLine(new[] { 'x', 'y' });
        await Assert.That(capture.Scenario.TearDownOutput).IsEqualTo("teardownxy<nl>");
        await Assert.That(capture.Original.ToString()).IsEqualTo("startupsliceline<nl>teardownxy<nl>");
    }

    [Test]
    public async Task Output_without_an_identity_is_only_forwarded()
    {
        using var capture = new Capture();
        TestOutputData.TestId = null;
        TestOutputData.FrameworkExecutionId = null;
        capture.Writer.WriteLine("outside");
        await Assert.That(capture.Writer.ToString()).IsEmpty();
        await Assert.That(capture.Original.ToString()).IsEqualTo("outside<nl>");
    }

    [Test]
    public async Task Parallel_async_identities_keep_all_write_forms_isolated_and_ordered()
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = Enumerable.Range(0, 16).Select(id => Task.Run(async () =>
        {
            using var capture = new Capture();
            await start.Task;
            var expected = new System.Text.StringBuilder();
            for (var i = 0; i < 20; i++)
            {
                var text = $"{id}:{i}";
                capture.Writer.Write(text);
                capture.Writer.Write(text.ToCharArray(), 0, text.Length);
                await capture.Writer.WriteLineAsync(text);
                capture.Writer.Write('!');
                expected.Append(text).Append(text).Append(text).Append("<nl>!");
                await Task.Yield();
            }
            await Assert.That(capture.Writer.ToString()).IsEqualTo(expected.ToString());
            await Assert.That(capture.Original.ToString()).IsEqualTo(expected.ToString());
        })).ToArray();
        start.SetResult();
        await Task.WhenAll(tasks);
    }

    [Test]
    public async Task Console_only_writes_use_the_capture_lock_without_capturing_summary_text()
    {
        using var capture = new Capture();
        var checkedWrites = 0;
        capture.Original.BeforeWrite = () =>
        {
            if (!Monitor.IsEntered(capture.Writer.SynchronizedWriter))
                throw new InvalidOperationException("Original console writes must acquire the capture monitor first.");
            checkedWrites++;
        };

        capture.Writer.SynchronizedWriter.Write("captured");
        capture.Writer.WriteLineToConsoleOnly("summary");

        await Assert.That(capture.Writer.ToString()).IsEqualTo("captured");
        await Assert.That(capture.Original.ToString()).IsEqualTo("capturedsummary" + capture.Original.NewLine);
        await Assert.That(checkedWrites).IsGreaterThanOrEqualTo(2);
    }

    private static string WriteForm(TextWriter writer, string form)
    {
        const string text = "bulk \uD83D\uDE80\ntext";
        switch (form)
        {
            case "string": writer.Write(text); return text;
            case "array": writer.Write(text.ToCharArray()); return text;
            case "slice": writer.Write(("-" + text + "-").ToCharArray(), 1, text.Length); return text;
            case "line": writer.WriteLine(text); return text + writer.NewLine;
            case "array-line": writer.WriteLine(text.ToCharArray()); return text + writer.NewLine;
            case "slice-line": writer.WriteLine(("-" + text + "-").ToCharArray(), 1, text.Length); return text + writer.NewLine;
            case "character-line": writer.WriteLine('c'); return "c" + writer.NewLine;
            default: writer.WriteLine(); return writer.NewLine;
        }
    }

    private sealed class Capture : IDisposable
    {
        private readonly string? _previousTestId = TestOutputData.TestId;
        private readonly string? _previousFrameworkId = TestOutputData.FrameworkExecutionId;
        private readonly BDTestRun TestRun = BDTestRun.Start();
        private readonly BDTestRun? _previousRun = TestOutputData.Run;
        private readonly TestDetails _details;
        public CountingWriter Original { get; } = new();
        public ConsoleOutputInterceptor Writer { get; }
        private Scenario? _scenario;
        public Scenario Scenario => Required(_scenario);

        public Capture(bool registerScenario = true)
        {
            _details = new TestDetails(TestRun);
            TestOutputData.Run = TestRun;
            Writer = new ConsoleOutputInterceptor(Original) { NewLine = "<nl>" };
            TestOutputData.TestId = _details.Guid;
            TestOutputData.FrameworkExecutionId = _details.TestId;
            if (registerScenario) RegisterScenario();
        }

        public void RegisterScenario()
        {
            _scenario = new Scenario(new List<Step>(), _details.Definition) { StartTime = DateTime.UtcNow, Status = Status.Pending };
        }

        public void Dispose()
        {
            TestOutputData.TestId = _details.Guid;
            TestOutputData.ClearCurrentTaskData();
            TestRun.Store.ScenariosByInternalId.TryRemove(Required(_details.Guid), out _);
            TestRun.Store.ScenariosByTestFrameworkId.TryRemove(Required(_details.TestId), out _);
            TestOutputData.TestId = _previousTestId;
            TestOutputData.FrameworkExecutionId = _previousFrameworkId;
            Original.Dispose();
            TestRun.Dispose();
            TestOutputData.Run = _previousRun;
        }
    }

    private sealed class TestDetails : BuildableTest
    {
        public TestDetails(BDTestRun run)
        {
            Run = run;
            Guid = System.Guid.NewGuid().ToString();
            TestId = System.Guid.NewGuid().ToString();
        }
    }

    private sealed class CountingWriter : StringWriter
    {
        public Action? BeforeWrite { get; set; }
        public int CharacterWrites { get; private set; }
        public int BulkWrites { get; private set; }
        public override void WriteLine(string? value) { BeforeWrite?.Invoke(); base.WriteLine(value); }
        public override void Write(char value) { CharacterWrites++; base.Write(value); }
        public override void Write(string? value) { BeforeWrite?.Invoke(); BulkWrites++; base.Write(value); }
        public override void Write(char[] buffer, int index, int count) { BulkWrites++; base.Write(buffer, index, count); }
    }
}
