namespace BDTest;

/// <summary>Selects the configuration used when an adapter starts this assembly's run.</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class BDTestConfigurationAttribute : Attribute
{
    public BDTestConfigurationAttribute(Type configurationType)
    {
        ConfigurationType = configurationType ?? throw new ArgumentNullException(nameof(configurationType));
        if (!typeof(IBDTestConfiguration).IsAssignableFrom(configurationType) || configurationType.IsAbstract
            || configurationType.ContainsGenericParameters || configurationType.GetConstructor(Type.EmptyTypes) == null)
            throw new ArgumentException("Configuration must implement IBDTestConfiguration and have a public parameterless constructor.", nameof(configurationType));
    }

    public Type ConfigurationType { get; }
}

/// <summary>Configures fresh options once per assembly run, before they are frozen.</summary>
public interface IBDTestConfiguration
{
    /// <remarks>Do not resolve another assembly run from this callback. A new configuration instance is created after reset.</remarks>
    void Configure(BDTestOptions options);
}
