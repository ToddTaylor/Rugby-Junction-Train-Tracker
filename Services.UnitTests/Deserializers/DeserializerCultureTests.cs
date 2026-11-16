using System.Globalization;
using Services.Deserializers;

namespace Services.UnitTests.Deserializers;

/// <summary>
/// SoftEOT and SoftDPU write the same format whatever the PC's regional settings, so a beacon PC set to a
/// culture with other date or decimal separators must read the logs the same way.
/// </summary>
[TestClass]
public class DeserializerCultureTests
{
    private const string HotLine = "2025/03/28-06:58:15  1.3 EOT  6720  88 1 1 OK   0 1 --- NML 1 0 CN   ------------";
    private const string DpuLine = "2025/03/29-15:42:57  0.5 57135  27 CM LD DI 18 1 - N8    FO 1 1 0 0 ---   REL ----- ----- --- ---   0  ----- ---- ---- ---- ---- ---                       ---- ------------";

    [TestMethod]
    [DataRow("en-US")]
    [DataRow("de-DE", DisplayName = "Comma decimals, dot date separator")]
    [DataRow("fr-FR", DisplayName = "Comma decimals")]
    [DataRow("fi-FI", DisplayName = "Dot date separator")]
    [DataRow("ar-SA", DisplayName = "Non-Gregorian calendar")]
    public void HotEot_ReadsTheSameUnderEveryCulture(string cultureName)
    {
        var packet = WithCulture(cultureName, () => HotEotDeserializer.Deserialize(HotLine));

        Assert.AreEqual(LocalToUtc(2025, 3, 28, 6, 58, 15), packet.TimeReceived);
        Assert.AreEqual(DateTimeKind.Utc, packet.TimeReceived.Kind);
        Assert.AreEqual(1.3m, packet.SIG);
        Assert.AreEqual(88, packet.BP);
    }

    [TestMethod]
    [DataRow("en-US")]
    [DataRow("de-DE")]
    [DataRow("fr-FR")]
    [DataRow("fi-FI")]
    [DataRow("ar-SA")]
    public void Dpu_ReadsTheSameUnderEveryCulture(string cultureName)
    {
        var packet = WithCulture(cultureName, () => DpuDeserializer.Deserialize(DpuLine));

        Assert.AreEqual(LocalToUtc(2025, 3, 29, 15, 42, 57), packet.TimeReceived);
        // Under a comma-decimal culture "0.5" was read as 5.
        Assert.AreEqual(0.5m, packet.SIG);
        Assert.AreEqual("27", packet.TRID);
    }

    private static DateTime LocalToUtc(int year, int month, int day, int hour, int minute, int second) =>
        new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local).ToUniversalTime();

    private static T WithCulture<T>(string cultureName, Func<T> action)
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(cultureName);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
