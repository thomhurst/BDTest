using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using BDTest.Test;
using HtmlTags;

namespace BDTest.ReportGenerator
{
    public static class Extensions
    {
        public static string ToPrettyFormat(this TimeSpan span)
        {
            if (span == TimeSpan.Zero) return "< 1 ms";

            var sb = new List<string>();
            if (span.Days > 0)
                sb.Add($"{span.Days} day{(span.Days > 1 ? "s" : string.Empty)} ");
            if (span.Hours > 0)
                sb.Add($"{span.Hours} hour{(span.Hours > 1 ? "s" : string.Empty)} ");
            if (span.Minutes > 0)
                sb.Add($"{span.Minutes} minute{(span.Minutes > 1 ? "s" : string.Empty)} ");
            if (span.Seconds > 0)
                sb.Add($"{span.Seconds} second{(span.Seconds > 1 ? "s" : string.Empty)} ");
            if (span.Milliseconds > 0)
                sb.Add($"{span.Milliseconds} ms");

            var joinedResult = string.Join(" | ", sb);
            return string.IsNullOrWhiteSpace(joinedResult) ? "< 1 ms" : joinedResult;
        }

        public static string ToXmlString(this XmlDocument xmlDocument)
        {
            using (var stringWriter = new StringWriter())
            using (var xmlTextWriter = XmlWriter.Create(stringWriter))
            {
                xmlDocument.WriteTo(xmlTextWriter);
                xmlTextWriter.Flush();
                return stringWriter.GetStringBuilder().ToString();
            }
        }

        public static DateTime GetStartDateTime(this IEnumerable<Scenario> scenarios)
        {
            return scenarios.OrderBy(scenario => scenario.StartTime).First().StartTime;
        }

        public static DateTime GetEndDateTime(this IEnumerable<Scenario> scenarios)
        {
            return scenarios.OrderByDescending(scenario => scenario.EndTime).First().EndTime;
        }

        public static double GetPercentage(this IEnumerable<Scenario> scenarios, Status status)
        {
            var enumerable = scenarios.ToList();
            double count = enumerable.Count(scenario => scenario.Status == status);
            var percentage = (count / enumerable.Count()) * 100;
            return Math.Round(percentage, 2);
        }

        public static double GetFlakinessPercentage(this IEnumerable<Scenario> scenarios)
        {
            var enumerable = scenarios.ToList();
            var enums = (Status[]) Enum.GetValues(typeof(Status));

            Enum.TryParse(enums.OrderByDescending(it => enumerable.Count(scenario => scenario.Status == it)).ToString(), out Status maxEnum);
            
            double count = enumerable.Count(scenario => scenario.Status == maxEnum);
            var percentage = (count / enumerable.Count()) * 100;
            if (percentage == 0)
            {
                return 0;
            }

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

        public static HtmlTag Append(this HtmlTag htmlTag, params HtmlTag[] htmlTags)
        {
            return htmlTag.Append(
                htmlTags.ToArray()
            );
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
    }
}
