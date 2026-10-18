namespace BDTest.Output;

// Keep diagnostics with the example until its scenario exists.
internal sealed class ScenarioConstructionOutput
{
    internal BufferedOutput Text { get; } = new();
    internal BufferedOutput Html { get; } = new();
    internal BufferedOutput Startup { get; } = new();
    internal BufferedOutput Teardown { get; } = new();
    internal List<Test.Attachment> Attachments { get; } = new();
    internal bool AttachmentCaptureCompleted { get; set; }
    internal BDTestRun Run { get; }
    internal string FrameworkExecutionId { get; }

    internal ScenarioConstructionOutput(string frameworkExecutionId, BDTestRun run)
    {
        Run = run;
        FrameworkExecutionId = frameworkExecutionId;
    }
}
