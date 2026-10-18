using System.Text;
using BDTest.Test;

namespace BDTest.Output;

internal class TestOutputData : TextWriter
{
    private static readonly AsyncLocal<BDTestRun> AsyncLocalRun = new();
    internal static BDTestRun Run
    {
        get => AsyncLocalRun.Value ?? BDTestRun.Default;
        set => AsyncLocalRun.Value = value;
    }
    private static Dictionary<string, StringBuilder> ThreadAndChars => Run.OutputBuffers;
    private static object Lock => Run.Store.Gate;
    public static readonly ConsoleOutputInterceptor Instance = new();
    private static TextWriter Console => Instance.One;

    private static readonly AsyncLocal<string> AsyncLocalTestId = new();
    internal static string TestId {
        get => AsyncLocalTestId.Value;
        set => AsyncLocalTestId.Value = value;
    }

    private static readonly AsyncLocal<string> AsyncLocalFrameworkExecutionId = new();
    public static string FrameworkExecutionId {
        get => AsyncLocalFrameworkExecutionId.Value;
        internal set => AsyncLocalFrameworkExecutionId.Value = value;
    }

    private static readonly AsyncLocal<ScenarioConstructionOutput> AsyncLocalConstructionOutput = new();
    internal static ScenarioConstructionOutput ConstructionOutput
    {
        get => AsyncLocalConstructionOutput.Value;
        set => AsyncLocalConstructionOutput.Value = value;
    }

    // xUnit constructs derived fixtures before their owned run is available.
    // Buffer console output without touching any run until initialization.
    private static readonly AsyncLocal<StartupOutputBuffer> AsyncLocalPendingStartupOutput = new();
    internal static StartupOutputBuffer PendingStartupOutput
    {
        get => AsyncLocalPendingStartupOutput.Value;
        set => AsyncLocalPendingStartupOutput.Value = value;
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
        var pending = PendingStartupOutput;
        if (pending != null)
        {
            pending.Write(value, character);
            return;
        }
        lock (Lock)
        {
            if (Run.Store.IsDisposed) return;
            // Builders change TestId as a chain grows. Keep construction output
            // in the example's buffer until its final scenario exists.
            if (ConstructionOutput != null && ConstructionOutput.Run == Run)
            {
                if (character.HasValue) ConstructionOutput.Text.Append(character.Value);
                else ConstructionOutput.Text.Append(value);
                return;
            }

            if (TestId == null && FrameworkExecutionId == null)
            {
                return;
            }

            // NUnit can run teardown in the setup flow, where only the framework
            // ID is present. Resolve the completed scenario before routing output.
            if (TestId == null && FrameworkExecutionId != null &&
                Run.Store.ScenariosByTestFrameworkId.TryGetValue(FrameworkExecutionId, out var completedScenario) &&
                completedScenario.EndTime != default)
            {
                TestId = completedScenario.Guid;
            }

            if (string.IsNullOrEmpty(TestId) 
                || !Run.Store.ScenariosByInternalId.TryGetValue(TestId, out var scenario)
                || scenario.Status == Status.NotRun)
            {
                if (FrameworkExecutionId == null)
                {
                    Console.WriteLine("Attempting to write test startup output but no unique test ID has been set in the base class");
                }
                else if (character.HasValue)
                {
                    Run.Store.AppendStartupOutput(FrameworkExecutionId, character.Value);
                }
                else
                {
                    Run.Store.AppendStartupOutput(FrameworkExecutionId, value, appendNewLine: false);
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

            if (TestId == null && Run.Store.ScenariosByTestFrameworkId.TryGetValue(FrameworkExecutionId, out var scenario))
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

    internal static void ClearAllData() => BDTestRun.Default.Clear();

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

    internal static void CollectStartupOutput(string frameworkExecutionId, string text, bool shouldWriteToConsole, bool appendNewLine = true, BDTestRun run = null)
    {
        if (frameworkExecutionId == null)
        {
            Console.WriteLine("Attempting to write test startup output but no unique test ID has been set in the base class");
            return;
        }

        run ??= Run;
        run.ThrowIfDisposed();
        appendNewLine = appendNewLine && text.Length > 1;
        var constructionOutput = ConstructionOutput;
        if (constructionOutput != null && constructionOutput.FrameworkExecutionId == frameworkExecutionId && constructionOutput.Run == run)
            constructionOutput.Startup.Append(text, appendNewLine);
        else
            run.Store.AppendStartupOutput(frameworkExecutionId, text, appendNewLine);

        if (shouldWriteToConsole)
        {
            Console.WriteLine(Environment.NewLine + text + (appendNewLine ? Environment.NewLine : string.Empty));
        }
    }

    internal static void CollectTearDownOutput(string frameworkExecutionId, string text, bool shouldWriteToConsole, BDTestRun run = null)
    {
        if (frameworkExecutionId == null)
        {
            Console.WriteLine("Attempting to write tear down output but no unique test ID has been set in the base class");
            return;
        }

        run ??= Run;
        run.ThrowIfDisposed();
        var constructionOutput = ConstructionOutput;
        if (constructionOutput != null && constructionOutput.FrameworkExecutionId == frameworkExecutionId && constructionOutput.Run == run)
        {
            constructionOutput.Teardown.Append(text, appendNewLine: true);
        }
        else if (run.Store.ScenariosByTestFrameworkId.TryGetValue(frameworkExecutionId, out var foundScenario))
        {
            foundScenario.TeardownOutputBuffer.Append(text, appendNewLine: true);
        }

        if (shouldWriteToConsole)
        {
            Console.WriteLine(Environment.NewLine + text);
        }
    }

    internal static void WriteCustomHtmlForReport(string testId, string htmlValue, BDTestRun run = null)
    {
        run ??= Run;
        run.ThrowIfDisposed();
        var constructionOutput = ConstructionOutput;
        if (constructionOutput != null && constructionOutput.FrameworkExecutionId == testId && constructionOutput.Run == run)
        {
            constructionOutput.Html.Append(htmlValue);
            return;
        }

        run.Store.ListenForScenario(testId, scenario => scenario.CustomHtmlOutputForReport += htmlValue);
    }
}
