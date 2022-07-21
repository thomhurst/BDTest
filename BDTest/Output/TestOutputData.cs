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

    public override void Write(char value)
    {
        lock (Lock)
        {
            if (TestId == null && FrameworkExecutionId == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(TestId) 
                || !TestHolder.ScenariosByInternalId.TryGetValue(TestId, out var scenario)
                || (scenario.Status == Status.Inconclusive && scenario.StartTime == default))
            {
                CollectStartupOutput(FrameworkExecutionId, value.ToString(), false);
                return;
            }

            if (scenario.EndTime != default)
            {
                scenario.TearDownOutput += value;
                return;
            }

            if (ThreadAndChars.TryGetValue(TestId, out var existingStringBuilder))
            {
                existingStringBuilder.Append(value);
            }
            else
            {
                ThreadAndChars.Add(TestId, new StringBuilder(value.ToString()));
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

    internal static void CollectStartupOutput(string frameworkExecutionId, string text, bool shouldWriteToConsole)
    {
        if (frameworkExecutionId == null)
        {
            Console.WriteLine("Attempting to write test startup output but no unique test ID has been set in the base class");
            return;
        }

        if (text.Length > 1)
        {
            text += Environment.NewLine;
        }
        
        TestHolder.ListenForScenario(frameworkExecutionId, scenario => scenario.TestStartupInformation += text);

        if (shouldWriteToConsole)
        {
            Console.WriteLine(Environment.NewLine + text);
        }
    }

    internal static void CollectTearDownOutput(string frameworkExecutionId, string text, bool shouldWriteToConsole)
    {
        if (frameworkExecutionId == null)
        {
            Console.WriteLine("Attempting to write tear down output but no unique test ID has been set in the base class");
            return;
        }

        if (TestHolder.ScenariosByTestFrameworkId.TryGetValue(frameworkExecutionId, out var foundScenario))
        {
            foundScenario.TearDownOutput += $"{text}{Environment.NewLine}";
        }

        if (shouldWriteToConsole)
        {
            Console.WriteLine(Environment.NewLine + text);
        }
    }

    internal static void WriteCustomHtmlForReport(string testId, string htmlValue)
    {
        if (TestHolder.ScenariosByTestFrameworkId.TryGetValue(testId, out var foundScenario))
        {
            foundScenario.CustomHtmlOutputForReport += htmlValue;
        }
        else
        {
            TestHolder.ListenForScenario(testId, scenario => scenario.CustomHtmlOutputForReport += htmlValue);
        }
    }
}