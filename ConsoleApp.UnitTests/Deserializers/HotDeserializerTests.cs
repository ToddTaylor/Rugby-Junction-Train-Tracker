using RailroadTelemetryLogService.ConsoleApp.Deserializers;
using RailroadTelemetryLogService.Models;

namespace ConsoleApp.UnitTests;

[TestClass]
public class HotDeserializerTests
{
    [TestMethod]
    public void Deserialize_Valid1_Test()
    {
        // Arrange
        var data = "2025/03/28-06:53:19  0.9 HOT  6720 --- - - -- --- - SRQ NML - - CN   ------------";
        var expected = new HotPacket
        {
            TimeReceived = new DateTime(2025, 3, 28, 6, 53, 19),
            EstimatedSignalStrength = 0.9m,
            Source = "HOT",
            ID = 6720,
            Command = "SRQ",
            MessageType = "NML",
            MovementRailroad = "CN",
        };

        // Act
        var actual = HotDeserializer.Deserialize(data);

        // Assert
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Deserialize_Valid2_Test()
    {
        // Arrange
        var data = "2025/03/28-06:58:15  1.3 EOT  6720  88 1 1 OK   0 1 --- NML 1 0 CN   ------------";
        var expected = new HotPacket
        {
            TimeReceived = new DateTime(2025, 3, 28, 6, 58, 15),
            EstimatedSignalStrength = 1.3m,
            Source = "EOT",
            ID = 6720,
            BreakPipePressure = 88,
            MotionStatus = 1,
            MarkerLightStatus = 1,
            BatteryStatus = "OK",
            BatteryChargeUsed = 0,
            AirTurbineEquipped = 1,
            MessageType = "NML",
            EmergencyValveHealth = 1,
            TwoWayLinkConfirmation = 0,
            MovementRailroad = "CN"
        };

        // Act
        var actual = HotDeserializer.Deserialize(data);

        // Assert
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Deserialize_Valid3_Test()
    {
        // Arrange
        var data = "2025/03/28-06:58:15  1.3 EOT  6720  88 1 1 OK   0 1 --- NML 1 0 WSOR T4H         ";
        var expected = new HotPacket
        {
            TimeReceived = new DateTime(2025, 3, 28, 6, 58, 15),
            EstimatedSignalStrength = 1.3m,
            Source = "EOT",
            ID = 6720,
            BreakPipePressure = 88,
            MotionStatus = 1,
            MarkerLightStatus = 1,
            BatteryStatus = "OK",
            BatteryChargeUsed = 0,
            AirTurbineEquipped = 1,
            MessageType = "NML",
            EmergencyValveHealth = 1,
            TwoWayLinkConfirmation = 0,
            MovementRailroad = "WSOR",
            MovementSymbol = "T4H"
        };

        // Act
        var actual = HotDeserializer.Deserialize(data);

        // Assert
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Deserialize_Invalid_Test()
    {
        // Arrange
        var data = "2025/03/28-09:04:33 ---- INV ----- --- - - -- --- - --- --- - - ---- ------------";
        var expected = new HotPacket
        {
            TimeReceived = new DateTime(2025, 3, 28, 9, 04, 33),
            Source = "INV"
        };

        // Act
        var actual = HotDeserializer.Deserialize(data);

        // Assert
        Assert.AreEqual(expected, actual);
    }
}
