namespace BDTest.Settings;

/// <summary>An immutable declaration of a framework's exception outcomes and runner notifications.</summary>
/// <remarks>Fluent methods return a new map. Register the completed map before starting a run.</remarks>
public sealed class FrameworkOutcomeMap
{
    private readonly Type[] _success;
    private readonly Type[] _skip;
    private readonly Type[] _inconclusive;
    private readonly Action<string> _reportSkip;
    private readonly Action<string> _reportInconclusive;

    public FrameworkOutcomeMap(Action<string> reportSkip, Action<string> reportInconclusive = null)
        : this(reportSkip, reportInconclusive, Array.Empty<Type>(), Array.Empty<Type>(), Array.Empty<Type>()) { }

    private FrameworkOutcomeMap(Action<string> reportSkip, Action<string> reportInconclusive,
        Type[] success, Type[] skip, Type[] inconclusive)
    {
        _reportSkip = reportSkip ?? throw new ArgumentNullException(nameof(reportSkip));
        _reportInconclusive = reportInconclusive ?? reportSkip;
        _success = success;
        _skip = skip;
        _inconclusive = inconclusive;
    }

    public FrameworkOutcomeMap Success<TException>() where TException : Exception =>
        new(_reportSkip, _reportInconclusive, Append(_success, typeof(TException)), _skip, _inconclusive);

    public FrameworkOutcomeMap Skip<TException>() where TException : Exception =>
        new(_reportSkip, _reportInconclusive, _success, Append(_skip, typeof(TException)), _inconclusive);

    public FrameworkOutcomeMap Inconclusive<TException>() where TException : Exception =>
        new(_reportSkip, _reportInconclusive, _success, _skip, Append(_inconclusive, typeof(TException)));

    public void ReportSkip(string reason) => _reportSkip(reason);
    public void ReportInconclusive(string reason) => _reportInconclusive(reason);

    internal void Register(Action<Type> addSuccess, IEnumerable<Type> success,
        Action<Type> addSkip, IEnumerable<Type> skip, Action<Type> addInconclusive, IEnumerable<Type> inconclusive)
    {
        AddMissing(_success, success, addSuccess);
        AddMissing(_skip, skip, addSkip);
        AddMissing(_inconclusive, inconclusive, addInconclusive);
    }

    private static Type[] Append(Type[] types, Type type) => types.Contains(type) ? types : types.Concat(new[] { type }).ToArray();

    private static void AddMissing(Type[] source, IEnumerable<Type> target, Action<Type> add)
    {
        var existing = new HashSet<Type>(target);
        foreach (var type in source)
            if (existing.Add(type)) add(type);
    }
}
