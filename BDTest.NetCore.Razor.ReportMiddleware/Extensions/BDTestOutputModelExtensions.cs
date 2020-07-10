using System;
using System.Linq;
using BDTest.Maps;
using BDTest.Test;

namespace BDTest.NetCore.Razor.ReportMiddleware.Extensions
{
    internal static class BDTestOutputModelExtensions
    {
        public static OutputMetrics ToOutputMetrics(this BDTestOutputModel bdTestOutputModel)
        {
            return bdTestOutputModel == null ? new OutputMetrics() : new OutputMetrics(bdTestOutputModel);
        }
    }

    public class OutputMetrics
    {
        public OutputMetrics()
        {
            
        }
        
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
                        break;
                    case Status.Inconclusive:
                        AnyInconclusive = true;
                        break;
                    case Status.Skipped:
                        AnySkipped = true;
                        break;
                    case Status.NotImplemented:
                        AnyNotImplemented = true;
                        break;
                    case Status.Passed:
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

        public bool AnyWarnings { get; set; }

        public bool AnyExceptions { get; }

        public bool AnySkipped { get; }

        public bool AnyNotImplemented { get; }

        public bool AnyInconclusive { get; }

        public bool AnyFailed { get; }
        public bool AllPassed => !AnyExceptions & !AnyFailed & !AnyInconclusive && !AnyNotImplemented & !AnySkipped;
    }
}