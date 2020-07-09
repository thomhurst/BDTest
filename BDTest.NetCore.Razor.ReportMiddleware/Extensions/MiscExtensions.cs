using System;
using System.Collections.Generic;
using System.Linq;
using BDTest.Test;

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

        public static double GetPercentage(this IEnumerable<Scenario> scenarios, Status status)
        {
            var enumerable = scenarios.ToList();
            double count = enumerable.Count(scenario => scenario.Status == status);
            var percentage = count / enumerable.Count() * 100;
            return Math.Round(percentage, 2);
        }

        public static double GetFlakinessPercentage(this IEnumerable<Scenario> scenarios)
        {
            var enumerable = scenarios.ToList();
            double enumerableCount = enumerable.Count;
            
            double passedScenarios = enumerable.Count(scenario => scenario.Status == Status.Passed);
            
            if (Math.Abs(enumerableCount - passedScenarios) < 1)
            {
                return 0;
            }

            var notPassed = enumerableCount - passedScenarios;

            var percentage = Math.Min(passedScenarios, notPassed) / enumerableCount * 100;

            return Math.Round(100 - percentage, 2);
        }

        public static int GetCount(this IEnumerable<Scenario> scenarios, Status status)
        {
            var enumerable = scenarios.ToList();
            return enumerable.Count(scenario => scenario.Status == status);
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

        public static string UsingHtmlNewLines(this string text)
        {
            return text.Replace(Environment.NewLine, "<br>");
        }
        
        public static string WrappedInSpan(this string text)
        {
            return $"<span>{text}</span>";
        }

        public static IEnumerable<string> SplitOnNewLines(this string text)
        {
            return text.Split(
                new[] { Environment.NewLine },
                StringSplitOptions.RemoveEmptyEntries
            );
        }

        public static string ReplaceHtmlCharacters(this string text)
        {
            return text.Replace("'", "&#39;")
                .Replace("`", "&#96;")
                .Replace("\"", "&#34;");
        }
        
        public static string EscapeQuotes(this string text)
        {
            return text.Replace("'", "\\'")
                .Replace("`", "\\`")
                .Replace("\"", "\\\"");
        }
    }
}
