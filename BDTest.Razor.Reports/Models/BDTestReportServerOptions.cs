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
        /// <summary>Optional attachment storage. Configure before serving requests; no attachment bytes are stored in report JSON.</summary>
        public IBDTestAttachmentStore? AttachmentStore { get; set; }
        /// <summary>Maximum attachment bytes in one report manifest (default 100 MiB).</summary>
        public long MaximumReportAttachmentBytes
        {
            get => _maximumReportAttachmentBytes;
            set => _maximumReportAttachmentBytes = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }
        private long _maximumReportAttachmentBytes = 100 * 1024 * 1024;
        /// <summary>Maximum bytes per attachment (default 10 MiB).</summary>
        public long MaximumAttachmentBytes
        {
            get => _maximumAttachmentBytes;
            set => _maximumAttachmentBytes = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }
        private long _maximumAttachmentBytes = 10 * 1024 * 1024;
        /// <summary>Maximum attachment count per report (default 1000).</summary>
        public int MaximumAttachmentsPerReport
        {
            get => _maximumAttachmentsPerReport;
            set => _maximumAttachmentsPerReport = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }
        private int _maximumAttachmentsPerReport = 1000;
        internal ReportAttachmentGate AttachmentGate { get; } = new();
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
        private long _maxCompressedUploadBytes = 10 * 1024 * 1024;
        private long _maxUploadBytes = 10 * 1024 * 1024;

        /// <summary>Positive upload request body limit before decompression (default 10 MiB). Also applies to uncompressed requests.</summary>
        public long MaxCompressedUploadBytes
        {
            get => _maxCompressedUploadBytes;
            set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
                _maxCompressedUploadBytes = value;
            }
        }

        /// <summary>Positive byte limit for decompressed JSON and its UTF-8 representation (default 10 MiB). Also bounds legacy reference expansion.</summary>
        public long MaxUploadBytes
        {
            get => _maxUploadBytes;
            set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
                _maxUploadBytes = value;
            }
        }

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
