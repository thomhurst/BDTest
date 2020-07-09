using System;
using System.Collections.Generic;

namespace BDTest.NetCore.Razor.ReportMiddleware.Extensions
{
    public static class MiscExtensions
    {
        public static string ToPrettyFormat(this TimeSpan span)
        {
            if (span == TimeSpan.Zero)
            {
                return "< 1 ms";
            }

            var sb = new List<string>();
            if (span.Days > 0)
            {
                sb.Add($"{span.Days} day{(span.Days > 1 ? "s" : string.Empty)} ");
            }

            if (span.Hours > 0)
            {
                sb.Add($"{span.Hours} hour{(span.Hours > 1 ? "s" : string.Empty)} ");
            }

            if (span.Minutes > 0)
            {
                sb.Add($"{span.Minutes} minute{(span.Minutes > 1 ? "s" : string.Empty)} ");
            }

            if (span.Seconds > 0)
            {
                sb.Add($"{span.Seconds} second{(span.Seconds > 1 ? "s" : string.Empty)} ");
            }

            if (span.Milliseconds > 0)
            {
                sb.Add($"{span.Milliseconds} ms");
            }

            var joinedResult = string.Join(" & ", sb);
            return string.IsNullOrWhiteSpace(joinedResult) ? "< 1 ms" : joinedResult;
        }

        public static string ToStringForReport(this DateTime dateTime)
        {
            return dateTime == DateTime.MinValue ? "" : dateTime.ToString("dd/MM/yyyy\nHH:mm:ss.FFF");
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>
            (this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            var seenKeys = new HashSet<TKey>();
            foreach (var element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }

        public static IEnumerable<string> SplitOnNewLines(this string text)
        {
            return text.Split(
                new[] { Environment.NewLine },
                StringSplitOptions.RemoveEmptyEntries
            );
        }
    }
}
