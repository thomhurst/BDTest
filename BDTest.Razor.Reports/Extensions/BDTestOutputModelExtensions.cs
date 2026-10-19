using BDTest.Contracts;

namespace BDTest.Razor.Reports.Extensions;

public class OutputMetrics
{
    public OutputMetrics(BDTestReport? bdTestOutputModel)
    {
        if (bdTestOutputModel == null)
        {
            return;
        }
            
        foreach (var scenario in bdTestOutputModel.Scenarios ?? Enumerable.Empty<ScenarioResult>())
        {
            switch (scenario.Status)
            {
                case Status.NotRun:
                    AnyNotRun = true;
                    AllPassed = false;
                    break;
                case Status.Pending:
                    AnyPending = true;
                    AllPassed = false;
                    break;
                case Status.Failed:
                    AnyFailed = true;
                    AllPassed = false;
                    break;
                case Status.Inconclusive:
                    AnyInconclusive = true;
                    AllPassed = false;
                    break;
                case Status.Skipped:
                    AnySkipped = true;
                    AllPassed = false;
                    break;
                case Status.NotImplemented:
                    AnyNotImplemented = true;
                    AllPassed = false;
                    break;
                case Status.Passed:
                    AnyPassed = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (scenario.Exception != null)
            {
                AnyExceptions = true;
            }
        }

        AnyWarnings = bdTestOutputModel.NotRun?.Any() == true;
    }

    public bool AnyNotRun { get; }

    public bool AnyPending { get; }

    public bool AnyPassed { get; }

    public bool AnyWarnings { get; }

    public bool AnyExceptions { get; }

    public bool AnySkipped { get; }

    public bool AnyNotImplemented { get; }

    public bool AnyInconclusive { get; }

    public bool AnyFailed { get; }
    public bool AllPassed { get; } = true;
}