using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using BDTest.Attributes;
using BDTest.Helpers;

namespace BDTest.Metadata;

// Cache by fixture type as well as method: inherited tests can have different class defaults.
// Weak keys allow collectible test assemblies to unload with their metadata.
internal sealed class TestMethodMetadata
{
    private static readonly ConditionalWeakTable<Type, ConcurrentDictionary<MethodInfo, Lazy<TestMethodMetadata>>> Methods = new();
    private static readonly ConditionalWeakTable<Type, TestMethodMetadata> Classes = new();
    private readonly string?[] _parameters;
    private readonly MethodInfo? _method;
    private readonly string[] _tags;

    internal string? ScenarioText { get; }
    internal int? RetryCount { get; }

    private TestMethodMetadata(Type fixtureType, MethodInfo? method)
    {
        ScenarioText = method?.GetCustomAttribute<ScenarioTextAttribute>(true)?.Text ?? NameHumanizer.Humanize(method?.Name);
        _parameters = method?.GetParameters().Select(parameter => parameter.Name).ToArray() ?? Array.Empty<string>();
        _method = method;
        _tags = fixtureType.GetCustomAttributes<BDTestTagAttribute>(true)
            .Concat(method?.GetCustomAttributes<BDTestTagAttribute>(true) ?? Enumerable.Empty<BDTestTagAttribute>())
            .Select(attribute => attribute.Tag).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        RetryCount = (method?.GetCustomAttribute<BDTestRetryAttribute>(true)
            ?? fixtureType.GetCustomAttribute<BDTestRetryAttribute>(true)
            ?? fixtureType.Assembly.GetCustomAttribute<BDTestRetryAttribute>())?.Count;
    }

    internal static TestMethodMetadata Get(Type fixtureType, MethodInfo? method) => method == null
        ? Classes.GetValue(fixtureType, type => new TestMethodMetadata(type, null))
        : Methods.GetValue(fixtureType, _ => new ConcurrentDictionary<MethodInfo, Lazy<TestMethodMetadata>>())
            .GetOrAdd(method, key => new Lazy<TestMethodMetadata>(() => new TestMethodMetadata(fixtureType, key))).Value;

    internal string?[] CopyParameters() => (string?[])_parameters.Clone();
    internal string[] CopyTags() => (string[])_tags.Clone();
    // Attribute subclasses can own arbitrary mutable state. Reconstruct them so scenarios never share it.
    internal TestInformationAttribute[] CopyInformation() =>
        _method?.GetCustomAttributes<TestInformationAttribute>(true).ToArray() ?? Array.Empty<TestInformationAttribute>();
}
