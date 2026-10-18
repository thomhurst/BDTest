namespace BDTest.Output;

// Keep diagnostics with the example until its scenario exists.
internal sealed class ScenarioConstructionOutput
{
    internal BufferedOutput Text { get; } = new();
    internal BufferedOutput Html { get; } = new();
    internal BufferedOutput Startup { get; } = new();
    internal BufferedOutput Teardown { get; } = new();
    internal string FrameworkExecutionId { get; }

    internal ScenarioConstructionOutput(string frameworkExecutionId)
    {
        FrameworkExecutionId = frameworkExecutionId;
    }
}
