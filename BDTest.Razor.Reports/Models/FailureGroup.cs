using BDTest.Contracts;

namespace BDTest.Razor.Reports.Models;

/// <summary>Failed scenarios that share a cause.</summary>
public class FailureGroup
{
    public const string ByExceptionAndLocation = "location";
    public const string ByExceptionType = "type";
    public const string ByFailingStep = "step";

    // Uploaded stack traces are untrusted: only the start of the trace is inspected.
    private const int MaximumStackTraceCharacters = 8192;
    private const int MaximumStackFrames = 50;

    public string Key { get; private init; } = string.Empty;

    /// <summary>Exception type name, or an empty string when the failure has no exception.</summary>
    public string TypeName { get; private init; } = string.Empty;

    /// <summary>The headline shown for the group: an exception message or a failing step.</summary>
    public string Title { get; private init; } = string.Empty;

    /// <summary>Where the exception was thrown, for example "CheckoutSteps.cs:line 84 · CheckoutSteps.Then".</summary>
    public string? Location { get; private init; }

    public ExceptionInfo? Exception { get; private init; }

    public IReadOnlyList<ScenarioResult> Scenarios { get; private init; } = Array.Empty<ScenarioResult>();

    public static IReadOnlyList<FailureGroup> Build(IEnumerable<ScenarioResult> scenarios, string? groupBy)
    {
        return scenarios
            .Where(scenario => scenario is { Status: Status.Failed })
            .SelectMany(scenario => (scenario.Exception?.GetRootCauses().Cast<ExceptionInfo?>() ?? new ExceptionInfo?[] { null })
                .GroupBy(exception => GetKey(scenario, exception, groupBy))
                .Select(group => (Scenario: scenario, Exception: group.First(), Key: group.Key)))
            .GroupBy(entry => entry.Key)
            .Select(group =>
            {
                var first = group.First();
                var exception = first.Exception;
                var step = FailingStep(first.Scenario);
                return new FailureGroup
                {
                    Key = group.Key,
                    TypeName = exception?.TypeName ?? string.Empty,
                    Title = groupBy switch
                    {
                        ByFailingStep => step?.StepText ?? "Failed outside a step",
                        ByExceptionType => exception?.TypeName ?? "No exception recorded",
                        _ => FirstLine(exception?.Message) ?? "No exception recorded"
                    },
                    Location = groupBy == ByExceptionType ? null : GetLocation(exception),
                    Exception = exception,
                    Scenarios = group.Select(entry => entry.Scenario).ToList()
                };
            })
            .OrderByDescending(group => group.Scenarios.Count)
            .ThenBy(group => group.Title, StringComparer.Ordinal)
            .ToList();
    }

    public static string Normalize(string? groupBy) => groupBy switch
    {
        ByExceptionType => ByExceptionType,
        ByFailingStep => ByFailingStep,
        _ => ByExceptionAndLocation
    };

    /// <summary>The step that failed, or the first step that recorded an exception.</summary>
    /// <remarks>Outlines keep their executed steps on their examples, so the first failed example is searched.</remarks>
    public static StepResult? FailingStep(ScenarioResult? scenario) => FindFailingStep(scenario).Step;

    /// <summary>The 1-based position of the failing step within its scenario or outline example, or 0.</summary>
    public static int FailingStepNumber(ScenarioResult? scenario)
    {
        var (step, owner) = FindFailingStep(scenario);
        return step == null ? 0 : (owner?.Steps?.ToList().IndexOf(step) ?? -1) + 1;
    }

    private static (StepResult? Step, ScenarioResult? Owner) FindFailingStep(ScenarioResult? scenario)
    {
        var steps = scenario?.Steps ?? new List<StepResult>();
        var step = steps.FirstOrDefault(s => s.Status == Status.Failed) ?? steps.FirstOrDefault(s => s.Exception != null);
        if (step != null || scenario?.Examples == null)
        {
            return (step, scenario);
        }

        foreach (var example in scenario.Examples.Where(example => example != null)
                     .OrderBy(example => example.Status.GetOrder()))
        {
            var found = FindFailingStep(example);
            if (found.Step != null)
            {
                return found;
            }
        }

        return (null, scenario);
    }

    /// <summary>The first stack frame with a source location, formatted as "File.cs:line 84 · Type.Method".</summary>
    public static string? GetLocation(ExceptionInfo? exception)
    {
        var stackTrace = exception?.StackTrace;
        if (string.IsNullOrEmpty(stackTrace))
        {
            return null;
        }

        string? fallback = null;
        var text = stackTrace.Length > MaximumStackTraceCharacters ? stackTrace[..MaximumStackTraceCharacters] : stackTrace;
        foreach (var rawLine in text.Split('\n').Take(MaximumStackFrames))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("at ", StringComparison.Ordinal))
            {
                continue;
            }

            var frame = line[3..];
            var inIndex = frame.LastIndexOf(" in ", StringComparison.Ordinal);
            var lineIndex = frame.LastIndexOf(":line ", StringComparison.Ordinal);
            var method = inIndex > 0 ? frame[..inIndex] : frame;
            var parenthesis = method.IndexOf('(');
            if (parenthesis > 0)
            {
                method = method[..parenthesis];
            }

            if (inIndex < 0 || lineIndex < inIndex)
            {
                fallback ??= method;
                continue;
            }

            var file = frame[(inIndex + 4)..lineIndex].Replace('\\', '/');
            file = file[(file.LastIndexOf('/') + 1)..];
            return $"{file}:line {frame[(lineIndex + 6)..]} · {method}";
        }

        return fallback;
    }

    private static string GetKey(ScenarioResult scenario, ExceptionInfo? exception, string? groupBy)
    {
        return groupBy switch
        {
            ByExceptionType => exception?.TypeName ?? string.Empty,
            ByFailingStep => FailingStep(scenario)?.StepText ?? string.Empty,
            _ => $"{exception?.TypeName}\n{FirstLine(exception?.Message)}\n{GetLocation(exception)}"
        };
    }

    private static string? FirstLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        var newLine = trimmed.IndexOfAny(new[] { '\r', '\n' });
        return newLine < 0 ? trimmed : trimmed[..newLine];
    }
}
