using System.Reflection;
using System.Runtime.CompilerServices;
using BDTest.Settings;

namespace BDTest;

/// <summary>Owns a framework's runs, keyed by the actual test assembly rather than its name.</summary>
/// <remarks>
/// Intended for adapter authors. Each adapter owns one registry. Call Dispose(assembly) after all tests,
/// output writers, attachment readers and report exports finish, including when reusing a test host.
/// Lookup and disposal are serialized; disposal does not wait for active tests. The next lookup starts
/// a fresh run. Never dispose the returned run directly; dispose it through this owner.
/// </remarks>
public sealed class BDTestAssemblyRuns
{
    private readonly object _gate = new();
    private readonly ConditionalWeakTable<Assembly, BDTestRun> _runs = new();
    private readonly FrameworkOutcomeMap _outcomes;
    [ThreadStatic] private static bool _configuring;

    public BDTestAssemblyRuns(FrameworkOutcomeMap outcomes) =>
        _outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));

    /// <summary>Returns the shared run, creating and configuring it once on first access.</summary>
    public BDTestRun GetRun(Assembly assembly)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
        if (_configuring) throw new InvalidOperationException("Assembly configuration cannot resolve assembly runs.");
        lock (_gate)
        {
            if (_runs.TryGetValue(assembly, out var existing)) return existing;
            var options = new BDTestOptions();
            _configuring = true;
            try
            {
                var configuration = assembly.GetCustomAttribute<BDTestConfigurationAttribute>();
                if (configuration != null)
                    ((IBDTestConfiguration)Activator.CreateInstance(configuration.ConfigurationType)!).Configure(options);
                options.RegisterFrameworkOutcomes(_outcomes);
                var run = BDTestRun.Start(options);
                _runs.Add(assembly, run);
                return run;
            }
            finally { _configuring = false; }
        }
    }

    /// <summary>Disposes and forgets an assembly's run. Repeated calls before another lookup are harmless.</summary>
    public void Dispose(Assembly assembly)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
        if (_configuring) throw new InvalidOperationException("Assembly configuration cannot dispose assembly runs.");
        lock (_gate)
        {
            if (!_runs.TryGetValue(assembly, out var run)) return;
            run.Dispose();
            _runs.Remove(assembly);
        }
    }
}
