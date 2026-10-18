using BDTest.Output;
using Newtonsoft.Json;

namespace BDTest.Test;

/// <summary>A completed scenario attempt, captured before retry state is reset.</summary>
public class ScenarioAttempt
{
    /// <summary>The one-based execution order, starting with the initial attempt.</summary>
    [JsonProperty] public int Number { get; private set; }

    [JsonProperty]
    public Status Status { get; private set; }

    [JsonProperty] public ExceptionWrapper Exception { get; private set; }
    [JsonProperty] public DateTime StartTime { get; private set; }
    [JsonProperty] public DateTime EndTime { get; private set; }

    [JsonProperty]
    [JsonConverter(typeof(TimespanConverter))]
    public TimeSpan TimeTaken { get; private set; }

    [JsonConstructor]
    private ScenarioAttempt()
    {
    }

    internal ScenarioAttempt(int number, Status status, Exception exception,
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
