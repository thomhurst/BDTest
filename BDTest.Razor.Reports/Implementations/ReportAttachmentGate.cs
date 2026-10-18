namespace BDTest.Razor.Reports.Implementations;

/// <summary>Coordinates attachment uploads and report cleanup within one report server.</summary>
// Only active holders and waiters retain an entry; report history cannot grow this map.
public sealed class ReportAttachmentGate
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    internal async Task<IDisposable> EnterAsync(string reportId, CancellationToken cancellationToken = default)
    {
        Entry? entry;
        lock (_entries)
        {
            if (!_entries.TryGetValue(reportId, out entry))
            {
                entry = new Entry();
                _entries.Add(reportId, entry);
            }
            entry.Users++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken);
            return new Lease(this, reportId, entry);
        }
        catch
        {
            RemoveUser(reportId, entry);
            throw;
        }
    }

    private void RemoveUser(string reportId, Entry entry)
    {
        lock (_entries)
        {
            if (--entry.Users == 0)
            {
                _entries.Remove(reportId);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class Entry
    {
        internal readonly SemaphoreSlim Semaphore = new(1, 1);
        internal int Users;
    }

    private sealed class Lease(ReportAttachmentGate owner, string reportId, Entry entry) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            entry.Semaphore.Release();
            owner.RemoveUser(reportId, entry);
        }
    }
}
