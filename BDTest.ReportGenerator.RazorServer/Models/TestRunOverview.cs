using System;
using BDTest.Test;

namespace BDTest.ReportGenerator.RazorServer.Models
{
    public class TestRunOverview
    {
        public TestRunOverview(string recordId, DateTime dateTime, Status status, string tag, string environment)
        {
            RecordId = recordId;
            DateTime = dateTime;
            Status = status;
            Tag = tag;
            Environment = environment;
        }

        public string RecordId { get; set; }
        public DateTime DateTime { get; set; }
        public Status Status { get; set; }
        public string Tag { get; set; }
        public string Environment { get; set; }
    }
}