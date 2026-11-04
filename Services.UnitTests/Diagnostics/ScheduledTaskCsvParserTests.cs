using FluentAssertions;
using Services.Diagnostics;
using System.Globalization;

namespace Services.UnitTests.Diagnostics;

[TestClass]
public class ScheduledTaskCsvParserTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    private const string Header =
        "\"HostName\",\"TaskName\",\"Next Run Time\",\"Status\",\"Logon Mode\",\"Last Run Time\",\"Last Result\",\"Author\",\"Task To Run\"";

    [TestMethod]
    public void Parse_ReadsLastRunTimeAndResult()
    {
        var csv = Header + "\r\n" +
            "\"RJ-SDR01\",\"\\Start SoftEOT\",\"N/A\",\"Ready\",\"Interactive only\",\"11/3/2026 6:02:00 AM\",\"1\",\"RJ-SDR01\\tracker\",\"C:\\Scripts\\StartSoftEOT.exe\"\r\n";

        var status = ScheduledTaskCsvParser.Parse("Start SoftEOT", csv, EnUs, TimeZoneInfo.Utc);

        status.Name.Should().Be("Start SoftEOT");
        status.Found.Should().BeTrue();
        status.LastRunAt.Should().Be(new DateTime(2026, 11, 3, 6, 2, 0, DateTimeKind.Utc));
        status.LastResult.Should().Be(1);
    }

    [TestMethod]
    public void Parse_TaskThatNeverRan_HasNoLastRunTime()
    {
        var csv = Header + "\n" +
            "\"RJ-SDR01\",\"\\Start SDR#\",\"N/A\",\"Ready\",\"Interactive only\",\"11/30/1999 12:00:00 AM\",\"267011\",\"x\",\"y\"\n";

        var status = ScheduledTaskCsvParser.Parse("Start SDR#", csv, EnUs, TimeZoneInfo.Utc);

        status.Found.Should().BeTrue();
        status.LastRunAt.Should().BeNull();
        status.LastResult.Should().Be(267011);
    }

    [TestMethod]
    public void Parse_HandlesCommasInsideQuotedFields()
    {
        var csv = Header + "\n" +
            "\"RJ-SDR01\",\"\\Start, then wait\",\"N/A\",\"Ready\",\"Interactive only\",\"11/3/2026 6:02:00 AM\",\"0\",\"x\",\"y\"\n";

        var status = ScheduledTaskCsvParser.Parse("Start, then wait", csv, EnUs, TimeZoneInfo.Utc);

        status.LastResult.Should().Be(0);
    }

    [TestMethod]
    public void Parse_EmptyOutput_IsNotFound()
    {
        var status = ScheduledTaskCsvParser.Parse("Missing", "", EnUs, TimeZoneInfo.Utc);

        status.Found.Should().BeFalse();
        status.LastRunAt.Should().BeNull();
        status.LastResult.Should().BeNull();
    }
}
