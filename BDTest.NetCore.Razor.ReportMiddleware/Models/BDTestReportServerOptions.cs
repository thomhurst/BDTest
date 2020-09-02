using BDTest.NetCore.Razor.ReportMiddleware.Implementations;
using BDTest.NetCore.Razor.ReportMiddleware.Interfaces;

namespace BDTest.NetCore.Razor.ReportMiddleware.Models
{
    public class BDTestReportServerOptions
    {
        internal BDTestReportServerOptions()
        {
        }
        
        public IBDTestDataStore DataStore { get; set; }
        public IBDTestCustomSidebarLinksProvider CustomSidebarLinksProvider { get; set; }
        public IBDTestCustomHeaderLinksProvider CustomHeaderLinksProvider { get; set; }
        public IAdminAuthorizer AdminAuthorizer { get; set; } = new NoOpAdminAuthorizer();
        public IBDTestDataReceiver DataReceiver { get; set; }
    }
}