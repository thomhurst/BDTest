using System.Text.RegularExpressions;
using BDTest.Test;

namespace BDTest.Razor.Reports.Models;

/// <summary>Failed scenarios that share a cause.</summary>
public class FailureGroup
{
    public const string ByExceptionAndLocation = "location";
    public const string ByExceptionType = "type";
    public const string ByFailingStep = "step";

    private static readonly Regex FrameRegex = new(@"^\s*at (?<method>.+?)(?: in (?<file>.+?):line (?<line>\d+))?\s*$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public string Key { get; private init; }

    /// <summary>Exception type name, or an empty string when the failure has no exception.</summary>
    public string TypeName { get; private init; }

    /// <summary>The headline shown for the group: an exception message or a failing step.</summary>
    public string Title { get; private init; }

    /// <summary>Where the exception was thrown, for example "CheckoutSteps.cs:line 84 · CheckoutSteps.Then".</summary>
    public string Location { get; private init; }

    public ExceptionWrapper Exception { get; private init; }

    public IReadOnlyList<Scenario> Scenarios { get; private init; }

    public static IReadOnlyList<FailureGroup> Build(IEnumerable<Scenario> scenarios, string groupBy)
    {
        return scenarios
            .Where(scenario => scenario is { Status: Status.Failed })
            .GroupBy(scenario => GetKey(scenario, groupBy))
            .Select(group =>
            {
                var first = group.First();
                var exception = first.Exception;
                var step = FailingStep(first);
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
                    Scenarios = group.ToList()
                };
            })
            .OrderByDescending(group => group.Scenarios.Count)
            .ThenBy(group => group.Title, StringComparer.Ordinal)
            .ToList();
    }

    public static string Normalize(string groupBy) => groupBy switch
    {
        ByExceptionType => ByExceptionType,
        ByFailingStep => ByFailingStep,
        _ => ByExceptionAndLocation
    };

    /// <summary>The step that failed, or the first step that recorded an exception.</summary>
    public static Step FailingStep(Scenario scenario)
    {
        var steps = scenario?.Steps ?? new List<Step>();
        return steps.FirstOrDefault(step => step.Status == Status.Failed)
               ?? steps.FirstOrDefault(step => step.Exception != null);
    }

    public static int FailingStepNumber(Scenario scenario)
    {
        var step = FailingStep(scenario);
        return step == null ? 0 : scenario.Steps.IndexOf(step) + 1;
    }

    /// <summary>The first stack frame with a source location, formatted as "File.cs:line 84 · Type.Method".</summary>
    public static string GetLocation(ExceptionWrapper exception)
    {
        var stackTrace = exception?.StackTrace;
        if (string.IsNullOrEmpty(stackTrace))
        {
            return null;
        }

        try
        {
            var frames = FrameRegex.Matches(stackTrace);
            var frame = frames.FirstOrDefault(match => match.Groups["file"].Success) ?? frames.FirstOrDefault();
            if (frame == null)
            {
                return null;
            }

            var method = frame.Groups["method"].Value;
            var parenthesis = method.IndexOf('(');
            if (parenthesis > 0)
            {
                method = method[..parenthesis];
            }

            if (!frame.Groups["file"].Success)
            {
                return method;
            }

            var file = frame.Groups["file"].Value.Replace('\\', '/');
            file = file[(file.LastIndexOf('/') + 1)..];
            return $"{file}:line {frame.Groups["line"].Value} · {method}";
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    private static string GetKey(Scenario scenario, string groupBy)
    {
        var exception = scenario.Exception;
        return groupBy switch
        {
            ByExceptionType => exception?.TypeName ?? string.Empty,
            ByFailingStep => FailingStep(scenario)?.StepText ?? string.Empty,
            _ => $"{exception?.TypeName}\n{FirstLine(exception?.Message)}\n{GetLocation(exception)}"
        };
    }

    private static string FirstLine(string text)
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
