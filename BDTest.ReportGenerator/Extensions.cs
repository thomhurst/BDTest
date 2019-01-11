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
            return scenarios.OrderByDescending(scenario => scenario.EndTime).First().StartTime;
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
    }
}
