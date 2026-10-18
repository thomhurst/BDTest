using System.Text;
using BDTest.Maps;
using BDTest.Test;

namespace BDTest.Output;

internal class TestOutputData : TextWriter
{
    private static readonly Dictionary<string, StringBuilder> ThreadAndChars = new();
    public static readonly ConsoleOutputInterceptor Instance = new();
    private static TextWriter Console => Instance.One;
    private static readonly object Lock = new();
    
    private static readonly AsyncLocal<string> AsyncLocalTestId = new();
    internal static string TestId {
        get => AsyncLocalTestId.Value;
        set => AsyncLocalTestId.Value = value;
    }

    private static readonly AsyncLocal<string> AsyncLocalFrameworkExecutionId = new();
    public static string FrameworkExecutionId {
        get => AsyncLocalFrameworkExecutionId.Value;
        set => AsyncLocalFrameworkExecutionId.Value = value;
    }

    private static readonly AsyncLocal<ScenarioConstructionOutput> AsyncLocalConstructionOutput = new();
    internal static ScenarioConstructionOutput ConstructionOutput
    {
        get => AsyncLocalConstructionOutput.Value;
        set => AsyncLocalConstructionOutput.Value = value;
    }

    public override void Write(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        WriteCore(value);
    }

    public override void Write(char[] buffer, int index, int count)
    {
        Write(new string(buffer, index, count));
    }

    public override void WriteLine() => Write(NewLine);

    public override void WriteLine(string value) => Write(value + NewLine);

    public override void WriteLine(char value) => Write(value + NewLine);

    public override void WriteLine(char[] buffer) => WriteLine(buffer == null ? null : new string(buffer));

    public override void WriteLine(char[] buffer, int index, int count) => WriteLine(new string(buffer, index, count));

    public override void Write(char value) => WriteCore(null, value);

    private static void WriteCore(string value, char? character = null)
    {
        lock (Lock)
        {
            // Builders change TestId as a chain grows. Keep construction output
            // in the example's buffer until its final scenario exists.
            if (ConstructionOutput != null)
            {
                if (character.HasValue) ConstructionOutput.Text.Append(character.Value);
                else ConstructionOutput.Text.Append(value);
                return;
            }

            if (TestId == null && FrameworkExecutionId == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(TestId) 
                || !TestHolder.ScenariosByInternalId.TryGetValue(TestId, out var scenario)
                || (scenario.Status == Status.Inconclusive && scenario.StartTime == default))
            {
                if (FrameworkExecutionId == null)
                {
                    Console.WriteLine("Attempting to write test startup output but no unique test ID has been set in the base class");
                }
                else if (character.HasValue)
                {
                    TestHolder.AppendStartupOutput(FrameworkExecutionId, character.Value);
                }
                else
                {
                    TestHolder.AppendStartupOutput(FrameworkExecutionId, value, appendNewLine: false);
                }
                return;
            }

            if (scenario.EndTime != default)
            {
                if (character.HasValue)
                {
                    scenario.TeardownOutputBuffer.Append(character.Value);
                }
                else
                {
                    scenario.TeardownOutputBuffer.Append(value);
                }
                return;
            }

            if (!ThreadAndChars.TryGetValue(TestId, out var existingStringBuilder))
            {
                existingStringBuilder = new StringBuilder();
                ThreadAndChars.Add(TestId, existingStringBuilder);
            }

            if (character.HasValue)
            {
                existingStringBuilder.Append(character.Value);
            }
            else
            {
                existingStringBuilder.Append(value);
            }
        }
    }

    public override string ToString()
    {
        lock (Lock)
        {
            if (TestId == null && FrameworkExecutionId == null)
            {
                return string.Empty;
            }

            if (TestId == null && TestHolder.ScenariosByTestFrameworkId.TryGetValue(FrameworkExecutionId, out var scenario))
            {
                TestId = scenario.Guid;
            }

            if (TestId == null)
            {
                return string.Empty;
            }

            return ThreadAndChars.TryGetValue(TestId, out var stringBuilder)
                ? stringBuilder.ToString()
                : string.Empty;
        }
    }

    internal static void ClearAllData()
    {
        // Use the same lock order as WriteCore: output, then scenario registry.
        lock (Lock)
        {
            ThreadAndChars.Clear();
            TestHolder.Clear();
        }
    }

    public static void ClearCurrentTaskData()
    {
        lock (Lock)
        {
            if (string.IsNullOrEmpty(TestId))
            {
                return;
            }
                
            ThreadAndChars.Remove(TestId);
        }
    }

    public override Encoding Encoding { get; } = Encoding.UTF8;

    internal static void CollectStartupOutput(string frameworkExecutionId, string text, bool shouldWriteToConsole, bool appendNewLine = true)
    {
        if (frameworkExecutionId == null)
        {
            Console.WriteLine("Attempting to write test startup output but no unique test ID has been set in the base class");
            return;
        }

        appendNewLine = appendNewLine && text.Length > 1;
        var constructionOutput = ConstructionOutput;
        if (constructionOutput != null && constructionOutput.FrameworkExecutionId == frameworkExecutionId)
            constructionOutput.Startup.Append(text, appendNewLine);
        else
            TestHolder.AppendStartupOutput(frameworkExecutionId, text, appendNewLine);

        if (shouldWriteToConsole)
        {
            Console.WriteLine(Environment.NewLine + text + (appendNewLine ? Environment.NewLine : string.Empty));
        }
    }

    internal static void CollectTearDownOutput(string frameworkExecutionId, string text, bool shouldWriteToConsole)
    {
        if (frameworkExecutionId == null)
        {
            Console.WriteLine("Attempting to write tear down output but no unique test ID has been set in the base class");
            return;
        }

        var constructionOutput = ConstructionOutput;
        if (constructionOutput != null && constructionOutput.FrameworkExecutionId == frameworkExecutionId)
        {
            constructionOutput.Teardown.Append(text, appendNewLine: true);
        }
        else if (TestHolder.ScenariosByTestFrameworkId.TryGetValue(frameworkExecutionId, out var foundScenario))
        {
            foundScenario.TeardownOutputBuffer.Append(text, appendNewLine: true);
        }

        if (shouldWriteToConsole)
        {
            Console.WriteLine(Environment.NewLine + text);
        }
    }

    internal static void WriteCustomHtmlForReport(string testId, string htmlValue)
    {
        var constructionOutput = ConstructionOutput;
        if (constructionOutput != null && constructionOutput.FrameworkExecutionId == testId)
        {
            constructionOutput.Html.Append(htmlValue);
            return;
        }

        TestHolder.ListenForScenario(testId, scenario => scenario.CustomHtmlOutputForReport += htmlValue);
    }
}
