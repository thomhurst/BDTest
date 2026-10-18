using System.Text.Json.Serialization;

namespace BDTest.Contracts;

/// <summary>An immutable structured exception node, including bounded diagnostic-tree metadata.</summary>
public sealed class ExceptionInfo
{
    public string TypeName { get; }
    public string Message { get; }
    public string? StackTrace { get; }
    public string? AsString { get; }
    public string ShortTypeName { get; }
    public IReadOnlyDictionary<string, string> Data { get; }
    public int? HResult { get; }
    public string? Source { get; }
    public IReadOnlyList<ExceptionInfo> InnerExceptions { get; }
    public bool IsWrapper { get; }
    public string? TruncationReason { get; }

    private static string GetShortTypeName(string typeName)
    {
        if (typeName == null) throw new ArgumentNullException(nameof(typeName));
        var argumentsStart = typeName.IndexOf('[');
        var name = argumentsStart < 0 ? typeName : typeName.Substring(0, argumentsStart);
        return name.Substring(Math.Max(name.LastIndexOf('.'), name.LastIndexOf('+')) + 1);
    }

    [JsonConstructor]
    public ExceptionInfo(
        string typeName,
        string message,
        string? stackTrace = null,
        string? asString = null,
        string? shortTypeName = null,
        IReadOnlyDictionary<string, string>? data = null,
        int? hResult = null,
        string? source = null,
        IReadOnlyList<ExceptionInfo>? innerExceptions = null,
        bool isWrapper = false,
        string? truncationReason = null)
    {
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        StackTrace = stackTrace;
        AsString = asString;
        ShortTypeName = shortTypeName ?? GetShortTypeName(typeName);
        Data = ContractCollections.Dictionary(data);
        HResult = hResult;
        Source = source;
        InnerExceptions = ContractCollections.List(innerExceptions);
        IsWrapper = isWrapper;
        TruncationReason = truncationReason;
    }
}
