using BDTest.Razor.Reports.Implementations;
using BDTest.Razor.Reports.Interfaces;
using Microsoft.Extensions.Logging;

namespace BDTest.Razor.Reports.Models;

public class BDTestReportServerOptions
{
        internal BDTestReportServerOptions()
        {
        }
#nullable enable
        /// <summary>Legacy model-based storage. Prefer ReportStore and RunIndexStore for new hosts.</summary>
        public IBDTestDataStore? DataStore { get; set; }
        public BDTest.Razor.Reports.Storage.IReportStore? ReportStore { get; set; }
        public BDTest.Razor.Reports.Storage.IRunIndexStore? RunIndexStore { get; set; }
#nullable enable
        public IBDTestCustomSidebarLinksProvider CustomSidebarLinksProvider { get; set; }
#nullable enable
        public IBDTestCustomHeaderLinksProvider CustomHeaderLinksProvider { get; set; }
#nullable enable
        public IAdminAuthorizer AdminAuthorizer { get; set; } = new NoOpAdminAuthorizer();
#nullable enable
        public IBDTestDataReceiver DataReceiver { get; set; }
#nullable enable
        public IUserPersonalizer UserPersonalizer { get; set; }
#nullable enable
        public ILogger? Logger { get; set; }

        private long _reportCacheSizeLimitBytes = 64 * 1024 * 1024;
        private TimeSpan _reportCacheRetention = TimeSpan.FromHours(8);

        /// <summary>
        /// Positive full-report cache budget, measured in serialized UTF-8 bytes (default 64 MiB).
        /// This is a size estimate, not a limit on process memory. Configure before serving requests.
        /// </summary>
        public long ReportCacheSizeLimitBytes
        {
            get => _reportCacheSizeLimitBytes;
            set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
                _reportCacheSizeLimitBytes = value;
            }
        }

        /// <summary>
        /// Positive full-report retention after upload or an ordinary read (default eight hours).
        /// Bulk reads do not extend retention. Capacity eviction may remove reports sooner.
        /// </summary>
        public TimeSpan ReportCacheRetention
        {
            get => _reportCacheRetention;
            set
            {
                if (value <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(value));
                _reportCacheRetention = value;
            }
        }

        /// <summary>
        /// Enables security headers on /bdtest responses. Disable only when the host supplies its own
        /// compatible policy. Register UseBDTestReportServer before endpoint/authentication middleware.
        /// </summary>
        public bool EnableSecurityHeaders { get; set; } = true;

        /// <summary>The secret required in the X-BDTest-Upload-Key header. Uploads are disabled when unset.</summary>
        public string? UploadApiKey { get; set; }

        /// <summary>Maximum authenticated upload attempts per minute, shared by all clients of this server instance.</summary>
        public int UploadsPerMinute { get; set; } = 60;
}