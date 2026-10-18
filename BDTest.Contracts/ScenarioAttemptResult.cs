using System.Text.Json.Serialization;

namespace BDTest.Contracts;

/// <summary>One completed execution attempt, in execution order.</summary>
public sealed class ScenarioAttemptResult
{
    public int Number { get; }
    public Status Status { get; }
    public ExceptionInfo? Exception { get; }
    public DateTimeOffset StartTime { get; }
    public DateTimeOffset EndTime { get; }
    public TimeSpan TimeTaken { get; }
    public IReadOnlyList<StepResult> Steps { get; }

    [JsonConstructor]
    public ScenarioAttemptResult(
        int number,
        Status status,
        ExceptionInfo? exception = null,
        DateTimeOffset startTime = default,
        DateTimeOffset endTime = default,
        TimeSpan timeTaken = default,
        IReadOnlyList<StepResult>? steps = null)
    {
        Number = number;
        Status = Enum.IsDefined(typeof(Status), status)
            ? status : throw new ArgumentOutOfRangeException(nameof(status), status, "Expected a defined status.");
        Exception = exception;
        StartTime = startTime;
        EndTime = endTime;
        TimeTaken = timeTaken;
        Steps = ContractCollections.List(steps);
    }
}
