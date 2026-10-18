using BDTest.Test;

namespace BDTest.Interfaces;

/// <summary>Asynchronous callbacks around each scenario attempt, including skips and retries.</summary>
/// <remarks>
/// Before hooks run in registration order before the instance hook. After hooks run after the
/// instance hook in registration order, even if a before hook or another after hook throws.
/// An original execution exception takes precedence. Implementations must be thread-safe.
/// </remarks>
public interface IScenarioHook
{
    Task BeforeScenarioAsync(ScenarioHookContext scenario);
    Task AfterScenarioAsync(ScenarioHookContext scenario);
}
