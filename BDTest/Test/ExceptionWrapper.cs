using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace BDTest.Test;

public class ExceptionWrapper
{
    /// <summary>Copies this exception tree to an immutable result, bounding cycles and depth.</summary>
    public Contracts.ExceptionInfo ToResult() => Helpers.ResultSnapshot.Create(this)!;

    private const int MaximumDepth = 10;
    private string? _shortTypeName;
    private bool _hasTruncation;

    [JsonInclude]
    public string? Message { get; set; }
    [JsonInclude]
    public string? StackTrace { get; set; }
    [JsonInclude]
    public string? TypeName { get; set; }

    /// <summary>The display name, derived from TypeName when reading historical reports.</summary>
    public string? ShortTypeName
    {
        get
        {
            if (_shortTypeName != null || TypeName == null) return _shortTypeName;
            var argumentsStart = TypeName.IndexOf('[');
            var name = argumentsStart < 0 ? TypeName : TypeName.Substring(0, argumentsStart);
            return name.Substring(Math.Max(name.LastIndexOf('.'), name.LastIndexOf('+')) + 1);
        }
        set => _shortTypeName = value;
    }

    public IReadOnlyDictionary<string, string>? Data { get; set; } = new Dictionary<string, string>();
    public int? HResult { get; set; }
    public string? Source { get; set; }
    public IReadOnlyList<ExceptionWrapper>? InnerExceptions { get; set; } = Array.Empty<ExceptionWrapper>();

    /// <summary>True for invocation wrappers and aggregates containing one child. Diagnostics are retained.</summary>
    public bool IsWrapper { get; set; }

    /// <summary>Cycle or DepthLimit when further traversal would be unsafe; otherwise null.</summary>
    public string? TruncationReason { get; set; }

    [JsonInclude]
    public string? AsString { get; set; }

    [JsonConstructor]
    internal ExceptionWrapper() { }

    public ExceptionWrapper(Exception exception) : this(exception, 0, new HashSet<Exception>(ReferenceComparer<Exception>.Instance)) { }

    private ExceptionWrapper(Exception exception, int depth, HashSet<Exception> path)
    {
        if (exception == null) throw new ArgumentNullException(nameof(exception));
        TypeName = exception.GetType().FullName ?? exception.GetType().Name;
        ShortTypeName = exception.GetType().Name;
        StackTrace = exception.StackTrace;
        try { Source = exception.Source; }
        catch { /* Optional diagnostics must not replace the original failure. */ }
        HResult = exception.HResult;
        Data = CaptureData(exception);
        var children = GetChildren(exception);
        IsWrapper = exception is TargetInvocationException || exception is AggregateException && children.Count == 1;

        if (!path.Add(exception)) TruncationReason = "Cycle";
        else
        {
            if (depth == MaximumDepth - 1 && children.Count > 0) TruncationReason = "DepthLimit";
            else InnerExceptions = children.Select(child => new ExceptionWrapper(child, depth + 1, path)).ToArray();
            path.Remove(exception);
        }

        _hasTruncation = TruncationReason != null || InnerExceptions.Any(child => child._hasTruncation);
        // AggregateException.Message also recursively reads its children's messages.
        Message = _hasTruncation && exception is AggregateException
            ? "Aggregate exception (message omitted because exception traversal was truncated)."
            : exception.Message;
        // Exception.ToString recursively follows InnerException and is unsafe for cyclic or very deep graphs.
        if (!_hasTruncation)
        {
            try { AsString = exception.ToString(); }
            catch { /* Custom exception formatters must not prevent reporting the failure. */ }
        }
        AsString ??= TypeName + ": " + Message + Environment.NewLine + StackTrace
            + (TruncationReason == null ? "" : Environment.NewLine + "[" + TruncationReason + "]")
            + string.Concat(InnerExceptions.Select(child => Environment.NewLine + child.AsString));
    }

    private static IReadOnlyList<Exception> GetChildren(Exception exception)
    {
        if (exception is AggregateException aggregate) return aggregate.InnerExceptions;
        if (exception.InnerException == null) return Array.Empty<Exception>();
        return new[] { exception.InnerException };
    }

    /// <summary>Returns every terminal cause, preserving all aggregate branches in their original order.</summary>
    public IReadOnlyList<ExceptionWrapper> GetRootCauses()
    {
        var causes = new List<ExceptionWrapper>();
        var visited = new HashSet<ExceptionWrapper>(ReferenceComparer<ExceptionWrapper>.Instance);
        var pending = new Stack<ExceptionWrapper>();
        pending.Push(this);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current)) continue;
            var children = current.InnerExceptions;
            if (children == null || children.Count == 0)
            {
                causes.Add(current);
                continue;
            }
            for (var index = children.Count - 1; index >= 0; index--)
            {
                if (children[index] != null) pending.Push(children[index]);
            }
        }
        if (causes.Count == 0) causes.Add(this);
        return causes;
    }

    private static IReadOnlyDictionary<string, string> CaptureData(Exception exception)
    {
        var result = new Dictionary<string, string>();
        try
        {
            var data = exception.Data;
            if (data == null) return result;
            foreach (DictionaryEntry entry in data)
            {
                var key = DisplayValue(entry.Key);
                var uniqueKey = key;
                for (var suffix = 2; result.ContainsKey(uniqueKey); suffix++) uniqueKey = key + " (" + suffix + ")";
                result.Add(uniqueKey, DisplayValue(entry.Value));
            }
        }
        catch { /* Optional diagnostics must not replace the original failure. Keep any entries already captured. */ }
        return result;
    }

    private static string DisplayValue(object? value)
    {
        // A Data value can refer back to a cyclic exception; do not call its recursive formatter.
        if (value is Exception exception) return exception.GetType().FullName ?? exception.GetType().Name;
        try { return value?.ToString() ?? ""; }
        catch { return "[Value could not be formatted]"; }
    }

    private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        internal static readonly ReferenceComparer<T> Instance = new();
        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
        public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value);
    }
}
