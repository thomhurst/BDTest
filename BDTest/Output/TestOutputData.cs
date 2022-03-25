using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using BDTest.Maps;

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

    public override void Write(char value)
    {
        lock (Lock)
        {
            if (TestId == null)
            {
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

    internal static void WriteTearDownOutput(string testId, string text)
    {
        if (testId == null)
        {
            Console.Out.WriteLine("Attempting to write tear down output but no unique test ID has been set in the base class");
            return;
        }

        if (TestHolder.ScenariosByTestFrameworkId.TryGetValue(testId, out var foundScenario))
        {
            foundScenario.TearDownOutput += $"{text}{Environment.NewLine}";
        }

        Console.Out.WriteLine(Environment.NewLine + text);
    }
        
    internal static void WriteStartupOutput(string testId, string text)
    {
        if (testId == null)
        {
            Console.Out.WriteLine("Attempting to write test startup output but no unique test ID has been set in the base class");
            return;
        }
            
        TestHolder.ListenForScenario(testId, scenario => scenario.TestStartupInformation += $"{text}{Environment.NewLine}");

        Console.Out.WriteLine(Environment.NewLine + text);
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