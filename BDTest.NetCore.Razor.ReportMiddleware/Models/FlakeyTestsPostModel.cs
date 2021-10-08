using System.Collections.Generic;

namespace BDTest.NetCore.Razor.ReportMiddleware.Models
{
    public class FlakeyTestsPostModel
    {
        public List<string> ReportIds { get; set; } = new();
    }
}