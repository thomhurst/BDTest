using System;
using System.Linq;
using BDTest.Maps;
using BDTest.Test;

namespace BDTest.NetCore.Razor.ReportMiddleware.Extensions;

public class OutputMetrics
{
    public OutputMetrics(BDTestOutputModel bdTestOutputModel)
    {
        if (bdTestOutputModel == null)
        {
            return;
        }
            
        foreach (var scenario in bdTestOutputModel.Scenarios)
        {
            switch (scenario.Status)
            {
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

        AnyWarnings = bdTestOutputModel.NotRun.Any();
    }

    public bool AnyPassed { get; }

    public bool AnyWarnings { get; }

    public bool AnyExceptions { get; }

    public bool AnySkipped { get; }

    public bool AnyNotImplemented { get; }

    public bool AnyInconclusive { get; }

    public bool AnyFailed { get; }
    public bool AllPassed { get; } = true;
}