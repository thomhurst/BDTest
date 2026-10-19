using System.IO.Compression;

namespace BDTest.Tests.Helpers;

// Exercise the shipped compatibility signatures without suppressing obsolete diagnostics.
internal static class CompatibilityReportUpload
{
    private static T Bind<T>(params Type[] parameters) where T : Delegate
        => (T)typeof(BDTestReportServer).GetMethod("SendDataAndGetReportUriAsync", parameters)!.CreateDelegate(typeof(T));

    public static Task<Uri> SendDataAndGetReportUriAsync(BDTestRun run, Uri address, BDTestRunDescriptor? descriptor)
        => Bind<Func<BDTestRun, Uri, BDTestRunDescriptor?, Task<Uri>>>(typeof(BDTestRun), typeof(Uri), typeof(BDTestRunDescriptor))(run, address, descriptor);

    public static Task<Uri> SendDataAndGetReportUriAsync(BDTestRun run, Uri address, BDTestRunDescriptor? descriptor, CancellationToken token)
        => Bind<Func<BDTestRun, Uri, BDTestRunDescriptor?, CancellationToken, Task<Uri>>>(typeof(BDTestRun), typeof(Uri), typeof(BDTestRunDescriptor), typeof(CancellationToken))(run, address, descriptor, token);

    public static Task<Uri> SendDataAndGetReportUriAsync(Uri address, BDTestRunDescriptor? descriptor, HttpClient client, BDTestRun run)
        => Bind<Func<Uri, BDTestRunDescriptor?, HttpClient, BDTestRun, Task<Uri>>>(typeof(Uri), typeof(BDTestRunDescriptor), typeof(HttpClient), typeof(BDTestRun))(address, descriptor, client, run);

    public static Task<Uri> SendDataAndGetReportUriAsync(Uri address, BDTestRunDescriptor? descriptor, HttpClient client, BDTestRun run, CancellationToken token)
        => Bind<Func<Uri, BDTestRunDescriptor?, HttpClient, BDTestRun, CancellationToken, Task<Uri>>>(typeof(Uri), typeof(BDTestRunDescriptor), typeof(HttpClient), typeof(BDTestRun), typeof(CancellationToken))(address, descriptor, client, run, token);

    public static async Task<string> ReadReportAsync(HttpContent content, CancellationToken token = default)
    {
        using var bytes = new MemoryStream(await content.ReadAsByteArrayAsync(token));
        using var gzip = content.Headers.ContentEncoding.Contains("gzip") ? new GZipStream(bytes, CompressionMode.Decompress) : null;
        using var reader = new StreamReader((Stream?)gzip ?? bytes);
        return await reader.ReadToEndAsync(token);
    }
}
