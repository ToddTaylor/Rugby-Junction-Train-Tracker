using ConsoleApp.Deserializers;
using ConsoleApp.Models;
using FluentAssertions;

namespace ConsoleApp.UnitTests.Deserializers;

[TestClass]
public class DpuDeserializerTests
{
    [TestMethod]
    public void Deserialize_Valid1_Test()
    {
        // Arrange
        var data = "2025/03/29-15:42:57  0.5 57135  27 CM LD DI 18 1 - N8    FO 1 1 0 0 ---   REL ----- ----- --- ---   0  ----- ---- ---- ---- ---- ---                       ---- ------------";
        var expected = new DpuPacket
        {
            TimeReceived = new DateTime(2025, 3, 29, 20, 42, 57), // UTC
            SIG = 0.5m,
            ADDR = "57135",
            TRID = "27",
            TP = "CM",
            OR = "LD",
            RP = "DI",
            SEQ = 18,
            NRM = 1,
            Power = "N8",
            REV = "FO",
            MTR = 1,
            BVIN = 1,
            Sand = 0,
            PRK = 0,
            BPRED = "REL",
            IB = 0
        };

        // Act
        var actual = DpuDeserializer.Deserialize(data);

        // Assert
        actual.Should().BeEquivalentTo(expected);
    }

    [TestMethod]
    public void Deserialize_Valid2_Test()
    {
        // Arrange
        var data = "2025/03/29-15:44:19  2.8 29080  27 ST RM DI 22 - 1 N8    FO 1 1 0 0  48 -----  89.5  91.0   0 133 ----   0.0 ---- ---- ---- ---- ---                       ---- ------------";
        var expected = new DpuPacket
        {
            TimeReceived = new DateTime(2025, 3, 29, 20, 44, 19), // UTC
            SIG = 2.8m,
            ADDR = "29080",
            TRID = "27",
            TP = "ST",
            OR = "RM",
            RP = "DI",
            SEQ = 22,
            RMID = 1,
            Power = "N8",
            REV = "FO",
            MTR = 1,
            BVIN = 1,
            Sand = 0,
            PRK = 0,
            TRE = 48,
            BP = 89.5m,
            ER = 91.0m,
            AF = 0,
            MR = 133,
            BC = 0.0m
        };

        // Act
        var actual = DpuDeserializer.Deserialize(data);

        // Assert
        actual.Should().BeEquivalentTo(expected);
    }

    [TestMethod]
    public void Deserialize_Invalid_Test()
    {
        // Arrange
        var data = "2025/03/29-15:41:58  0.4   INV --- -- -- -- -- - - ----- -- - - - - --- ----- ----- ----- --- --- ---- ----- ---- ---- ---- ---- ---                       ---- ------------";
        var expected = new DpuPacket
        {
            TimeReceived = new DateTime(2025, 3, 29, 20, 41, 58),
            SIG = 0.4m,
            ADDR = "INV"
        };

        // Act
        var actual = DpuDeserializer.Deserialize(data);

        // Assert
        actual.Should().BeEquivalentTo(expected);
    }
}