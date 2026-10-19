using System.Text;
using BDTest.Reports.Storage.FileSystem;
using Microsoft.Extensions.Options;

namespace BDTest.Tests.Helpers;

// Invoked directly by the cross-process regression test, never by the test runner.
internal static class FileSystemStoreWorker
{
    public static async Task Main(string[] args)
    {
        if (args.Length != 3 || args[0] != "file-store-worker")
            throw new ArgumentException("Expected file-store-worker <writer|contender> <directory>.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = timeout.Token;
        var store = new FileSystemReportStore(Options.Create(new FileSystemReportStoreOptions
        {
            DirectoryPath = args[2], Retention = TimeSpan.FromDays(1)
        }));
        if (args[1] == "writer")
        {
            using var input = new HeldStream();
            await store.WriteAsync("run", input, token);
            Console.WriteLine("written");
        }
        else if (args[1] == "contender")
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            var listing = store.ListAsync(new(), cancellation.Token);
            RequirePending(listing);
            Console.WriteLine("waiting-to-cancel");
            await Command("cancel", token);
            RequirePending(listing);
            cancellation.Cancel();
            try
            {
                await listing;
                throw new InvalidOperationException("Waiting operation did not cancel.");
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                Console.WriteLine("cancelled");
            }
            var reading = store.OpenReadAsync("run", token);
            RequirePending(reading);
            Console.WriteLine("waiting-to-read");
            await using var report = await reading ?? throw new InvalidOperationException("Report missing.");
            using var reader = new StreamReader(report);
            Console.WriteLine("complete:" + await reader.ReadToEndAsync(token));
        }
        else throw new ArgumentException("Unknown worker mode.");
    }

    private static void RequirePending(Task operation)
    {
        if (operation.IsCompleted) throw new InvalidOperationException("Operation bypassed the other process's lock.");
    }

    private static async Task Command(string expected, CancellationToken token)
    {
        if (await Console.In.ReadLineAsync(token) != expected)
            throw new InvalidOperationException("Unexpected worker command.");
    }

    private sealed class HeldStream : MemoryStream
    {
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            await destination.WriteAsync(Encoding.UTF8.GetBytes("partial"), cancellationToken);
            await destination.FlushAsync(cancellationToken);
            Console.WriteLine("holding");
            await Command("release", cancellationToken);
            await destination.WriteAsync(Encoding.UTF8.GetBytes("-complete"), cancellationToken);
        }
    }
}
