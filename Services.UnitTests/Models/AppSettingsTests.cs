using FluentAssertions;
using Services.Models;
using System.Text.Json;

namespace Services.UnitTests.Models;

[TestClass]
public class AppSettingsTests
{
    [TestMethod]
    public void AddMissingMonitoredProcesses_SettingsSavedBeforeSlices_AddsSlicesAndKeepsSavedEntries()
    {
        // Settings saved by a ConsoleApp that monitored only these three programs, one of them changed by hand.
        const string savedJson = """
            {
              "LogDirectoryPath": "C:\\TrainTracker\\Logs",
              "MonitoredProcesses": [
                { "Name": "SDR#", "ProcessName": "SDRSharp*" },
                { "Name": "SoftEOT", "ProcessName": "SoftEOT_Custom" },
                { "Name": "SoftDPU", "ProcessName": "SoftDPU" }
              ]
            }
            """;
        var settings = JsonSerializer.Deserialize<AppSettings>(savedJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        settings.MonitoredProcesses.Select(p => p.Name).Should().NotContain("SDR# slices");

        settings.AddMissingMonitoredProcesses();

        settings.MonitoredProcesses.Select(p => p.Name).Should().Equal("SDR#", "SoftEOT", "SoftDPU", "SDR# slices");
        settings.MonitoredProcesses.Single(p => p.Name == "SoftEOT").ProcessName.Should().Be("SoftEOT_Custom");
        settings.MonitoredProcesses.Single(p => p.Name == "SDR# slices").WindowTitleContains.Should().Be("Sharp Slicer");
    }

    [TestMethod]
    public void AddMissingMonitoredProcesses_DefaultsOrNull_LeavesOneOfEach()
    {
        var settings = new AppSettings();
        settings.AddMissingMonitoredProcesses();
        settings.MonitoredProcesses.Should().HaveCount(AppSettings.DefaultMonitoredProcesses().Count);

        settings.MonitoredProcesses = null!;
        settings.AddMissingMonitoredProcesses();
        settings.MonitoredProcesses.Should().HaveCount(AppSettings.DefaultMonitoredProcesses().Count);
    }
}
