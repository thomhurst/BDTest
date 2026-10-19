using System.Collections;
using System.Reflection;

namespace BDTest.TestUtilities;

// Preserve the NUnit cleanup regressions without granting another assembly friend
// access. These reads intentionally fail when the inspected implementation changes.
internal static class OutputCaptureInspection
{
    private static readonly Type OutputData = typeof(BDTestRun).Assembly.GetType("BDTest.Output.TestOutputData", throwOnError: true)!;

    public static string? FrameworkExecutionId => (string?)Property(OutputData, "FrameworkExecutionId").GetValue(null);
    public static string? TestId => (string?)Property(OutputData, "TestId").GetValue(null);

    public static IDictionary Buffers(BDTestRun run) =>
        (IDictionary)(Property(typeof(BDTestRun), "OutputBuffers").GetValue(run)
            ?? throw new InvalidOperationException("Expected the run's output buffers."));

    private static PropertyInfo Property(Type type, string name) =>
        type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
        ?? throw new InvalidOperationException($"Expected {type.FullName}.{name}.");
}
