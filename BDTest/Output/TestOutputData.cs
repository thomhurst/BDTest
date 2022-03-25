using System.Text;
using BDTest.Maps;
using BDTest.Reporters;
using BDTest.Test;

namespace BDTest.Output;

internal class TestOutputData : TextWriter
{
    private static readonly Dictionary<Guid, StringBuilder> ThreadAndChars = new();
    public static readonly ConsoleOutputInterceptor Instance = new();
    private static readonly object Lock = new();
    
    private static readonly AsyncLocal<Guid?> AsyncLocalTestId = new();
    internal static Guid? TestId {
        get => AsyncLocalTestId.Value;
        set => AsyncLocalTestId.Value = value;
    }

    private static readonly AsyncLocal<string?> AsyncLocalFrameworkExecutionId = new();
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

            if (TestId == null 
                || !TestHolder.ScenariosByInternalId.TryGetValue(TestId.ToString(), out var scenario)
                || (scenario.Status == Status.Inconclusive && scenario.StartTime == default))
            {
                WriteStartupOutput(FrameworkExecutionId, value.ToString());
                return;
            }

            if (scenario.EndTime != default)
            {
                scenario.TearDownOutput += value;
                return;
            }

            if (ThreadAndChars.TryGetValue((Guid) TestId, out var existingStringBuilder))
            {
                existingStringBuilder.Append(value);
            }
            else
            {
                ThreadAndChars.Add((Guid) TestId, new StringBuilder(value.ToString()));
            }
        }
    }

    public override string ToString()
    {
        lock (Lock)
        {
            if (TestId == null)
            {
                return string.Empty;
            }
                
            return ThreadAndChars.TryGetValue((Guid) TestId, out var stringBuilder)
                ? stringBuilder.ToString()
                : string.Empty;
        }
    }

    public static void ClearCurrentTaskData()
    {
        lock (Lock)
        {
            if (TestId == null)
            {
                return;
            }
                
            ThreadAndChars.Remove((Guid) TestId);
        }
    }

    public override Encoding Encoding { get; } = Encoding.UTF8;

    internal static void WriteTearDownOutput(string frameworkExecutionId, string text)
    {
        if (frameworkExecutionId == null)
        {
            Console.Out.WriteLine("Attempting to write tear down output but no unique test ID has been set in the base class");
            return;
        }

        if (TestHolder.ScenariosByTestFrameworkId.TryGetValue(frameworkExecutionId, out var foundScenario))
        {
            foundScenario.TearDownOutput += $"{text}{Environment.NewLine}";
        }

        Console.Out.WriteLine(Environment.NewLine + text);
    }
        
    internal static void WriteStartupOutput(string frameworkExecutionId, string text)
    {
        if (frameworkExecutionId == null)
        {
            ConsoleReporter.WriteLineToConsoleOnly("Attempting to write test startup output but no unique test ID has been set in the base class");
            return;
        }

        if (text.Length > 1)
        {
            text += Environment.NewLine;
        }
        
        TestHolder.ListenForScenario(frameworkExecutionId, scenario => scenario.TestStartupInformation += text);

        ConsoleReporter.WriteLineToConsoleOnly(Environment.NewLine + text);
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