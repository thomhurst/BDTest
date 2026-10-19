namespace BDTest.Test;

public enum Status
{
    /// <summary>Defined but not started, including steps that were never reached.</summary>
    NotRun,
    /// <summary>Execution has started and has not produced a result yet.</summary>
    Pending,
    Passed,
    Failed,
    /// <summary>Execution ended with a registered inconclusive exception.</summary>
    Inconclusive,
    NotImplemented,
    Skipped
}

public static class StatusExtensions
{
    public static int GetOrder(this Status status) => status switch
    {
        Status.Failed => 1,
        Status.NotImplemented => 2,
        Status.Inconclusive => 3,
        Status.Skipped => 4,
        Status.NotRun => 5,
        Status.Pending => 6,
        Status.Passed => 7,
        _ => int.MaxValue
    };
}
