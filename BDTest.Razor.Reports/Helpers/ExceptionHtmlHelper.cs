using BDTest.Contracts;

namespace BDTest.Razor.Reports.Helpers;

/// <summary>Exception comparison and grouping for hosted page projections.</summary>
internal static class ExceptionHtmlHelper
{
    // JSON carries values, not object identity. Compare every diagnostic field so
    // distinct failures still render even when their type and message are identical.
    internal static bool HasSameDetails(ExceptionInfo? left, ExceptionInfo? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left == null || right == null) return false;

        var pending = new Stack<(ExceptionInfo Left, ExceptionInfo Right)>();
        pending.Push((left, right));
        while (pending.Count > 0)
        {
            var (first, second) = pending.Pop();
            if (ReferenceEquals(first, second)) continue;
            if (first.TypeName != second.TypeName || first.Message != second.Message
                || first.StackTrace != second.StackTrace || first.AsString != second.AsString
                || first.ShortTypeName != second.ShortTypeName || first.HResult != second.HResult
                || first.Source != second.Source || first.IsWrapper != second.IsWrapper
                || first.TruncationReason != second.TruncationReason
                || first.Data.Count != second.Data.Count
                || first.InnerExceptions.Count != second.InnerExceptions.Count)
                return false;

            foreach (var pair in first.Data)
                if (!second.Data.TryGetValue(pair.Key, out var value) || pair.Value != value)
                    return false;
            for (var index = 0; index < first.InnerExceptions.Count; index++)
                pending.Push((first.InnerExceptions[index], second.InnerExceptions[index]));
        }
        return true;
    }

    internal static IReadOnlyList<ExceptionInfo> GetRootCauses(this ExceptionInfo exception)
    {
        var causes = new List<ExceptionInfo>();
        var pending = new Stack<ExceptionInfo>();
        pending.Push(exception);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current.InnerExceptions.Count == 0) causes.Add(current);
            else for (var index = current.InnerExceptions.Count - 1; index >= 0; index--)
                pending.Push(current.InnerExceptions[index]);
        }
        return causes;
    }
}
