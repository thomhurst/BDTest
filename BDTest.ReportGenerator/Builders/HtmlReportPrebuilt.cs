using System;
using System.Collections.Generic;
using System.Linq;
using BDTest.Test;
using HtmlTags;

namespace BDTest.ReportGenerator.Builders
{
    public static class HtmlReportPrebuilt
    {
        public static HtmlTag PassedIcon => new DivTag().AppendHtml(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\">\r\n    <title>Passed</title><path fill=\"none\" d=\"M0 0h24v24H0z\"/>\r\n    <path fill=\"#8fcf8f\" d=\"M9 16.2L4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4L9 16.2z\"/>\r\n</svg>");

        public static HtmlTag FailedIcon => new DivTag().AppendHtml(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\">\r\n    <title>Failed</title><path fill=\"none\" d=\"M0 0h24v24H0V0z\"/>\r\n    <path fill=\"#e27d7a\" d=\"M11 15h2v2h-2zm0-8h2v6h-2zm.99-5C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8z\"/>\r\n</svg>");

        public static HtmlTag InconclusiveIcon => new DivTag().AppendHtml(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\">\r\n    <title>Inconclusive</title><path fill=\"none\" d=\"M0 0h24v24H0z\"/>\r\n    <path fill=\"#ffa700\" d=\"M11 18h2v-2h-2v2zm1-16C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm0-14c-2.21 0-4 1.79-4 4h2c0-1.1.9-2 2-2s2 .9 2 2c0 2-3 1.75-3 5h2c0-2.25 3-2.5 3-5 0-2.21-1.79-4-4-4z\"/>\r\n</svg>");

        public static HtmlTag NotImplementedIcon => new DivTag().AppendHtml(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\">\r\n    <title>Not Implemented</title><path clip-rule=\"evenodd\" fill=\"none\" d=\"M0 0h24v24H0z\"/>\r\n    <path fill=\"#4285F4\" d=\"M22.7 19l-9.1-9.1c.9-2.3.4-5-1.5-6.9-2-2-5-2.4-7.4-1.3L9 6 6 9 1.6 4.7C.4 7.1.9 10.1 2.9 12.1c1.9 1.9 4.6 2.4 6.9 1.5l9.1 9.1c.4.4 1 .4 1.4 0l2.3-2.3c.5-.4.5-1.1.1-1.4z\"/>\r\n</svg>");

        private static HtmlTag TableHeader(string title)
        {
            return new HtmlTag("th").AppendText(title);
        }

        public static HtmlTag StoryHeader => TableHeader("Story");
        public static HtmlTag ScenarioHeader => TableHeader("Scenario");
        public static HtmlTag StepHeader => TableHeader("Step");
        public static HtmlTag StatusHeader => TableHeader("Status");
        public static HtmlTag StoriesHeader => TableHeader("Stories");
        public static HtmlTag ScenariosHeader => TableHeader("Scenarios");
        public static HtmlTag DurationHeader => TableHeader("Duration");
        public static HtmlTag StartHeader => TableHeader("Start");
        public static HtmlTag EndHeader => TableHeader("End");

        public static string GetStatus(Scenario scenario)
        {
            return GetStatus(scenario.Status);
        }

        public static string GetStatus(Step step)
        {
            return GetStatus(step.Status);
        }

        public static string GetStatus(Status status)
        {
            return status.ToString();
        }

        internal static string GetStatus(IEnumerable<Scenario> scenarios)
        {
            var statuses = scenarios.Select(scenario => scenario.Status).ToList();
            if (statuses.Contains(Status.Failed))
            {
                return "Failed";
            }

            if (statuses.Contains(Status.Inconclusive))
            {
                return "Inconclusive";
            }

            if (statuses.Contains(Status.NotImplemented))
            {
                return "NotImplemented";
            }

            return "Passed";
        }

        public static HtmlTag GetStatusIcon(Scenario scenario)
        {
            return GetStatusIcon(scenario.Status);
        }

        public static HtmlTag GetStatusIcon(Step step)
        {
            return GetStatusIcon(step.Status);
        }

        public static HtmlTag GetStatusIcon(Status status)
        {
            switch (status)
            {
                case Status.Passed:
                    return PassedIcon;
                case Status.Failed:
                    return FailedIcon;
                case Status.Inconclusive:
                    return InconclusiveIcon;
                case Status.NotImplemented:
                    return NotImplementedIcon;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }

        internal static HtmlTag GetStatusIcon(IEnumerable<Scenario> scenarios)
        {
            var statuses = scenarios.Select(scenario => scenario.Status).ToList();
            if (statuses.Contains(Status.Failed))
            {
                return FailedIcon;
            }

            if (statuses.Contains(Status.Inconclusive))
            {
                return InconclusiveIcon;
            }

            if (statuses.Contains(Status.NotImplemented))
            {
                return NotImplementedIcon;
            }

            return PassedIcon;
        }
    }
}
