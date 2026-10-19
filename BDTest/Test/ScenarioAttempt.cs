using BDTest.Contracts;
using BDTest.Output;
using System.Text.Json.Serialization;

namespace BDTest.Test;

/// <summary>A completed scenario attempt, captured before retry state is reset.</summary>
public class ScenarioAttempt
{
    /// <summary>The one-based execution order, starting with the initial attempt.</summary>
    [JsonInclude] public int Number { get; internal set; }

    [JsonInclude]
    [JsonConverter(typeof(StatusConverter))]
    public Status Status { get; internal set; }

    [JsonInclude] public ExceptionWrapper? Exception { get; internal set; }
    [JsonInclude] public DateTime StartTime { get; internal set; }
    [JsonInclude] public DateTime EndTime { get; internal set; }

    [JsonInclude]
    [JsonConverter(typeof(TimespanConverter))]
    public TimeSpan TimeTaken { get; internal set; }

    [JsonIgnore] internal IReadOnlyList<StepResult> Steps { get; set; } = Array.Empty<StepResult>();

    [JsonConstructor]
    internal ScenarioAttempt()
    {
    }

    internal ScenarioAttempt(int number, Status status, Exception? exception,
        DateTime startTime, DateTime endTime, TimeSpan timeTaken)
    {
        Number = number;
        Status = status;
        Exception = exception == null ? null : new ExceptionWrapper(exception);
        StartTime = startTime;
        EndTime = endTime;
        TimeTaken = timeTaken;
    }
}
