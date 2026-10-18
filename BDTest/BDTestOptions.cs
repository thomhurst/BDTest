using BDTest.Interfaces;
using BDTest.Settings;
using BDTest.Settings.Retry;
using BDTest.Settings.Skip;

namespace BDTest;

/// <summary>Registrations to copy when starting an explicit test run.</summary>
/// <remarks>
/// Configure before calling BDTestRun.Start. Later changes affect only subsequently started runs.
/// Callback, hook and converter instances are shared with the snapshot and must provide their own
/// thread safety. Do not modify this object concurrently with Start.
/// </remarks>
public sealed class BDTestOptions
{
    public IList<Type> SuccessExceptionTypes { get; } = new List<Type>();
    public IList<Type> SkipExceptionTypes { get; } = new List<Type>();
    public IList<Type> InconclusiveExceptionTypes { get; } = new List<Type>();
    internal FrameworkOutcomeMap FrameworkOutcomes { get; private set; }

    /// <summary>Registers exception types once and selects this map's runner notifications.</summary>
    /// <remarks>Call before Start. When registering several maps, the last map supplies notifications.</remarks>
    public void RegisterFrameworkOutcomes(FrameworkOutcomeMap map)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        map.Register(SuccessExceptionTypes.Add, SuccessExceptionTypes, SkipExceptionTypes.Add, SkipExceptionTypes,
            InconclusiveExceptionTypes.Add, InconclusiveExceptionTypes);
        FrameworkOutcomes = map;
    }

    public RetryTestRules RetryTestRules { get; } = new();
    public SkipTestRules SkipTestRules { get; } = new();
    public SkipStepRules SkipStepRules { get; } = new();
    public DebugSettings Debug { get; } = new();

    /// <summary>Hooks run sequentially in registration order, around the test instance hooks.</summary>
    public IList<IStepHook> StepHooks { get; } = new List<IStepHook>();

    private readonly List<StepTextConverterRegistration> _converters = new();
    internal IReadOnlyList<StepTextConverterRegistration> Converters => _converters;

    /// <summary>Registers a typed converter. The first registration matching an argument wins.</summary>
    /// <remarks>Conversion uses the registered T contract, including explicit interface implementations.</remarks>
    public void RegisterCustomStringConverter<T>(IStepTextStringConverter<T> converter)
    {
        if (converter == null) throw new ArgumentNullException(nameof(converter));
        _converters.Add(new StepTextConverterRegistration(typeof(T), value => converter.ConvertToString((T)value)));
    }
}

internal sealed class StepTextConverterRegistration(Type valueType, Func<object, string> convert)
{
    internal bool Matches(object value) => valueType.IsInstanceOfType(value);
    internal string Convert(object value) => convert(value);
}
