using System.Reflection;
using BDTest.Settings;
using BDTest.Test;

namespace BDTest.Helpers;

internal static class ExceptionOutcomeClassifier
{
    internal static Status Classify(Exception exception, RunOptions options, bool timedOut = false)
    {
        if (timedOut) return Status.Failed;

        // Only transparent, single-cause wrappers are unwrapped. Multi-inner (and
        // empty) aggregates are classified as aggregates, never by one chosen cause.
        while (true)
        {
            if (exception is TargetInvocationException { InnerException: not null } invocation)
                exception = invocation.InnerException;
            else if (exception is AggregateException aggregate && aggregate.InnerExceptions.Count == 1)
                exception = aggregate.InnerExceptions[0];
            else
                break;
        }

        if (exception is NotImplementedException) return Status.NotImplemented;
        if (Matches(options.SuccessExceptionTypes, exception)) return Status.Passed;
        if (Matches(options.SkipExceptionTypes, exception)) return Status.Skipped;
        if (Matches(options.InconclusiveExceptionTypes, exception)) return Status.Inconclusive;
        return Status.Failed;
    }

    private static bool Matches(IEnumerable<Type> types, Exception exception) =>
        types.Any(type => type?.IsInstanceOfType(exception) == true);
}
