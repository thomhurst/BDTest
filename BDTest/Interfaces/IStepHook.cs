using BDTest.Contracts;
using BDTest.Test;

namespace BDTest.Interfaces;

/// <summary>A run's asynchronous callbacks for each reached step, including skipped steps and retries.</summary>
/// <remarks>
/// Before hooks run in registration order before BDTestBase.OnBeforeStep. After hooks run in
/// registration order after BDTestBase.OnAfterStep. All after hooks run even when another hook fails;
/// the original execution failure takes precedence. Use cancellationToken for cooperative cancellation. Implementations must provide their own thread safety.
/// </remarks>
public interface IStepHook
{
    Task BeforeStepAsync(StepResult step, System.Threading.CancellationToken cancellationToken);
    Task AfterStepAsync(StepResult step, System.Threading.CancellationToken cancellationToken);
}
