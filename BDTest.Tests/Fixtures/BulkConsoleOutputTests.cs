using BDTest.Maps;
using BDTest.Output;
using BDTest.Test;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

[NonParallelizable]
public class BulkConsoleOutputTests
{
    [TestCase("string")]
    [TestCase("array")]
    [TestCase("slice")]
    [TestCase("line")]
    [TestCase("array-line")]
    [TestCase("slice-line")]
    [TestCase("character-line")]
    [TestCase("empty-line")]
    public void Bulk_writes_preserve_text_and_forward_without_character_calls(string form)
    {
        using var capture = new Capture();
        var expected = WriteForm(capture.Writer, form);

        Assert.That(capture.Writer.ToString(), Is.EqualTo(expected));
        Assert.That(capture.Original.ToString(), Is.EqualTo(expected));
        Assert.That(capture.Original.CharacterWrites, Is.Zero);
        Assert.That(capture.Original.BulkWrites, Is.EqualTo(1));
    }

    [TestCase("string")]
    [TestCase("array")]
    [TestCase("slice")]
    [TestCase("line")]
    [TestCase("array-line")]
    [TestCase("slice-line")]
    [TestCase("character-line")]
    [TestCase("empty-line")]
    public void Capture_writer_accepts_bulk_writes_directly(string form)
    {
        using var capture = new Capture();
        capture.Writer.Two.NewLine = "<capture-nl>";
        var expected = WriteForm(capture.Writer.Two, form);
        Assert.That(capture.Writer.ToString(), Is.EqualTo(expected));
        Assert.That(capture.Original.ToString(), Is.Empty);
    }

    [Test]
    public void Character_null_empty_and_mixed_writes_preserve_order()
    {
        using var capture = new Capture();
        capture.Writer.Write('a');
        capture.Writer.Write((string)null);
        capture.Writer.Write((char[])null);
        capture.Writer.Write("");
        capture.Writer.Write(Array.Empty<char>(), 0, 0);
        capture.Writer.Write("bc");
        capture.Writer.Write(new[] { 'x', 'd', 'e', 'x' }, 1, 2);
        capture.Writer.WriteLine((string)null);
        capture.Writer.WriteLine((char[])null);
        capture.Writer.WriteLine("f");
        var expected = "abcde<nl><nl>f<nl>";
        Assert.That(capture.Writer.ToString(), Is.EqualTo(expected));
        Assert.That(capture.Original.ToString(), Is.EqualTo(expected));
        Assert.That(capture.Original.CharacterWrites, Is.EqualTo(1));
    }

    [Test]
    public void Invalid_slices_do_not_capture_or_forward_any_text()
    {
        using var capture = new Capture();
        Assert.Throws<ArgumentNullException>(() => capture.Writer.Write(null, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => capture.Writer.Write(new char[2], -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => capture.Writer.Write(new char[2], 0, -1));
        Assert.Catch<ArgumentException>(() => capture.Writer.Write(new char[2], 1, 2));
        Assert.Catch<ArgumentException>(() => capture.Writer.WriteLine(new char[2], 1, 2));
        Assert.That(capture.Writer.ToString(), Is.Empty);
        Assert.That(capture.Original.ToString(), Is.Empty);
    }

    [Test]
    public void Startup_and_teardown_capture_raw_chunks_without_extra_newlines()
    {
        using var capture = new Capture(registerScenario: false);
        capture.Writer.Write("startup");
        capture.Writer.Write(new[] { '-', 's', 'l', 'i', 'c', 'e', '-' }, 1, 5);
        capture.Writer.WriteLine("line");
        capture.RegisterScenario();
        Assert.That(capture.Scenario.TestStartupInformation, Is.EqualTo("startupsliceline<nl>"));

        capture.Scenario.EndTime = DateTime.UtcNow;
        capture.Writer.Write("teardown");
        capture.Writer.WriteLine(new[] { 'x', 'y' });
        Assert.That(capture.Scenario.TearDownOutput, Is.EqualTo("teardownxy<nl>"));
        Assert.That(capture.Original.ToString(), Is.EqualTo("startupsliceline<nl>teardownxy<nl>"));
    }

    [Test]
    public void Output_without_an_identity_is_only_forwarded()
    {
        using var capture = new Capture();
        TestOutputData.TestId = null;
        TestOutputData.FrameworkExecutionId = null;
        capture.Writer.WriteLine("outside");
        Assert.That(capture.Writer.ToString(), Is.Empty);
        Assert.That(capture.Original.ToString(), Is.EqualTo("outside<nl>"));
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
            Assert.That(capture.Writer.ToString(), Is.EqualTo(expected.ToString()));
            Assert.That(capture.Original.ToString(), Is.EqualTo(expected.ToString()));
        })).ToArray();
        start.SetResult();
        await Task.WhenAll(tasks);
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
        private readonly string _previousTestId = TestOutputData.TestId;
        private readonly string _previousFrameworkId = TestOutputData.FrameworkExecutionId;
        private readonly TestDetails _details = new();
        public CountingWriter Original { get; } = new();
        public ConsoleOutputInterceptor Writer { get; }
        public Scenario Scenario { get; private set; }

        public Capture(bool registerScenario = true)
        {
            Writer = new ConsoleOutputInterceptor(Original) { NewLine = "<nl>" };
            TestOutputData.TestId = _details.Guid;
            TestOutputData.FrameworkExecutionId = _details.TestId;
            if (registerScenario) RegisterScenario();
        }

        public void RegisterScenario()
        {
            Scenario = new Scenario(new List<Step>(), _details) { StartTime = DateTime.UtcNow };
        }

        public void Dispose()
        {
            TestOutputData.TestId = _details.Guid;
            TestOutputData.ClearCurrentTaskData();
            TestHolder.ScenariosByInternalId.TryRemove(_details.Guid, out _);
            TestHolder.ScenariosByTestFrameworkId.TryRemove(_details.TestId, out _);
            TestOutputData.TestId = _previousTestId;
            TestOutputData.FrameworkExecutionId = _previousFrameworkId;
            Original.Dispose();
        }
    }

    private sealed class TestDetails : BuildableTest
    {
        public TestDetails()
        {
            Guid = System.Guid.NewGuid().ToString();
            TestId = System.Guid.NewGuid().ToString();
        }
    }

    private sealed class CountingWriter : StringWriter
    {
        public int CharacterWrites { get; private set; }
        public int BulkWrites { get; private set; }
        public override void Write(char value) { CharacterWrites++; base.Write(value); }
        public override void Write(string value) { BulkWrites++; base.Write(value); }
        public override void Write(char[] buffer, int index, int count) { BulkWrites++; base.Write(buffer, index, count); }
    }
}
