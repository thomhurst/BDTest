using System;
using BDTest.Test;

namespace BDTest.NetCore.Razor.ReportMiddleware.Models
{
    public class TestRunSummary
    {
        public TestRunSummary(string recordId,
            DateTime startedAtDateTime,
            DateTime finishedAtDateTime,
            Status status,
            int totalCount,
            int failedCount,
            string tag,
            string environment, 
            string version
            )
        {
            RecordId = recordId;
            StartedAtDateTime = startedAtDateTime;
            FinishedAtDateTime = finishedAtDateTime;
            Status = status;
            TotalCount = totalCount;
            FailedCount = failedCount;
            Tag = tag;
            Environment = environment;
            Version = version;
        }

        public string RecordId { get; set; }
        public DateTime StartedAtDateTime { get; set; }
        public DateTime FinishedAtDateTime { get; }
        public Status Status { get; set; }
        public int TotalCount { get; set; }
        public int FailedCount { get; set; }
        public string Tag { get; set; }
        public string Environment { get; set; }
        public string Version { get; set; }
    }
}