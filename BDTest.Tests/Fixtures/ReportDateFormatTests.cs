using System.Globalization;
using System.Text.RegularExpressions;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Models;
using BDTest.Test;
using Newtonsoft.Json;
using NUnit.Framework;

namespace BDTest.Tests.Fixtures;

public class ReportDateFormatTests
{
    private static readonly DateTime Timestamp = new(2026, 10, 17, 14, 5, 6, 123, DateTimeKind.Utc);

    [TestCase("en-GB", "17/10/2026")]
    [TestCase("en-US", "10/17/2026")]
    [TestCase("de-DE", "17.10.2026")]
    public void Report_dates_use_the_current_cultures_short_date(string cultureName, string expectedDate)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            Assert.That(Timestamp.ToStringForReport(), Is.EqualTo(expectedDate + "\n14:05:06.123"));
            Assert.That(Timestamp.ToStringForReport(false), Is.EqualTo(expectedDate + " 14:05:06.123"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Unset_dates_remain_empty(bool includeLineBreak)
    {
        Assert.That(DateTime.MinValue.ToStringForReport(includeLineBreak), Is.Empty);
    }

    [TestCase(0, "14:05:06")]
    [TestCase(120, "14:05:06.12")]
    public void Time_precision_is_preserved(int milliseconds, string expectedTime)
    {
        var timestamp = Timestamp.AddMilliseconds(milliseconds - Timestamp.Millisecond);
        Assert.That(timestamp.ToStringForReport(false), Does.EndWith(" " + expectedTime));
    }
}
