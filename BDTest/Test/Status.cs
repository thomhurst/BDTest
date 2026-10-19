namespace BDTest.Test;

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
