using System.Globalization;
using System.Text.RegularExpressions;
using BDTest.Razor.Reports.Extensions;
using BDTest.Razor.Reports.Models;
using BDTest.Test;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace BDTest.Tests.Fixtures;

public class ReportDateFormatTests
{
    private static readonly DateTime Timestamp = new(2026, 10, 17, 14, 5, 6, 123, DateTimeKind.Utc);

    [Test]
    [Arguments("en-GB", "17/10/2026")]
    [Arguments("en-US", "10/17/2026")]
    [Arguments("de-DE", "17.10.2026")]
    public async Task Report_dates_use_the_current_cultures_short_date(string cultureName, string expectedDate)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            await Assert.That(Timestamp.ToStringForReport()).IsEqualTo(expectedDate + "\n14:05:06.123");
            await Assert.That(Timestamp.ToStringForReport(false)).IsEqualTo(expectedDate + " 14:05:06.123");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Unset_dates_remain_empty(bool includeLineBreak)
    {
        await Assert.That(DateTime.MinValue.ToStringForReport(includeLineBreak)).IsEmpty();
    }

    [Test]
    [Arguments(0, "14:05:06")]
    [Arguments(120, "14:05:06.12")]
    public async Task Time_precision_is_preserved(int milliseconds, string expectedTime)
    {
        var timestamp = Timestamp.AddMilliseconds(milliseconds - Timestamp.Millisecond);
        await Assert.That(timestamp.ToStringForReport(false)).EndsWith(" " + expectedTime);
    }
}
