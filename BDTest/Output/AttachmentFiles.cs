using BDTest.Settings;
using BDTest.Test;

namespace BDTest.Output;

// Each run owns its directory, quota and ledger, including in-progress copies.
internal sealed class AttachmentFiles
{
    private sealed class Entry(string path)
    {
        internal string Path { get; } = path;
        internal long Length;
        internal bool Completed;
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _files = new();
    private readonly long _maximumAttachmentBytes;
    private readonly long _maximumRunBytes;
    private long _totalBytes;
    private long _generation;

    internal long Generation { get { lock (_gate) return _generation; } }

    internal string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "BDTest", Guid.NewGuid().ToString("N"));

    internal AttachmentFiles(AttachmentOptions options)
    {
        if (options.MaximumAttachmentBytes < 0) throw new ArgumentOutOfRangeException(nameof(options.MaximumAttachmentBytes));
        if (options.MaximumRunBytes < 0) throw new ArgumentOutOfRangeException(nameof(options.MaximumRunBytes));
        _maximumAttachmentBytes = options.MaximumAttachmentBytes;
        _maximumRunBytes = options.MaximumRunBytes;
    }

    internal Attachment Save(string name, Stream source, string mimeType, long generation)
    {
        var id = Guid.NewGuid().ToString("N");
        var entry = new Entry(Path.Combine(DirectoryPath, id));
        try
        {
            FileStream destination;
            lock (_gate)
            {
                if (generation != _generation) throw new InvalidOperationException("The attachment run has been cleared.");
                Directory.CreateDirectory(DirectoryPath);
                destination = new FileStream(entry.Path, FileMode.CreateNew, FileAccess.Write, FileShare.Read | FileShare.Delete);
                _files.Add(id, entry);
            }
            using (destination)
            {
                var buffer = new byte[81920];
                int count;
                while ((count = source.Read(buffer, 0, buffer.Length)) != 0)
                {
                    lock (_gate)
                    {
                        GetEntry(id);
                        if (count > _maximumAttachmentBytes - entry.Length || count > _maximumRunBytes - _totalBytes)
                            throw new IOException("The attachment exceeds the configured attachment or run byte limit.");
                        entry.Length += count;
                        _totalBytes += count;
                    }
                    destination.Write(buffer, 0, count);
                }
            }
            lock (_gate)
            {
                GetEntry(id);
                entry.Completed = true;
            }
            return new Attachment { Id = id, Name = name, MimeType = mimeType, Length = entry.Length };
        }
        catch
        {
            Remove(id);
            throw;
        }
    }

    internal void EnsureAvailable(string id)
    {
        lock (_gate)
        {
            if (!GetEntry(id).Completed) throw new InvalidOperationException("Attachment capture is incomplete.");
        }
    }

    internal Stream Open(string id)
    {
        lock (_gate)
        {
            EnsureAvailable(id);
            return new FileStream(_files[id].Path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        }
    }

    private Entry GetEntry(string id) => _files.TryGetValue(id, out var entry)
        ? entry : throw new KeyNotFoundException("The attachment does not belong to this run or has been cleared.");

    internal void Remove(string id)
    {
        lock (_gate)
        {
            if (!_files.TryGetValue(id, out var entry)) return;
            File.Delete(entry.Path);
            _totalBytes -= entry.Length;
            _files.Remove(id);
        }
    }

    internal void Clear()
    {
        lock (_gate)
        {
            if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, recursive: true);
            _files.Clear();
            _totalBytes = 0;
            _generation++;
        }
    }
}
