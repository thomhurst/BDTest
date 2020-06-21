using System;
using BDTest.Test;

namespace BDTest.ReportGenerator.RazorServer.Models
{
    public class RecordDateTimeModel
    {
        public RecordDateTimeModel(string recordId, DateTime dateTime, Status status)
        {
            RecordId = recordId;
            DateTime = dateTime;
            Status = status;
        }

        public string RecordId { get; set; }
        public DateTime DateTime { get; set; }
        public Status Status { get; set; }
    }
}