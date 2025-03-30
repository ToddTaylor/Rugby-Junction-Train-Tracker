using ConsoleApp.Deserializers;
using ConsoleApp.Models;

namespace ConsoleApp.UnitTests.Deserializers;

[TestClass]
public class HotEotDeserializerTests
{
    [TestMethod]
    public void Deserialize_Valid1_Test()
    {
        // Arrange
        var data = "2025/03/28-06:53:19  0.9 HOT  6720 --- - - -- --- - SRQ NML - - CN   ------------";
        var expected = new HotEotPacket
        {
            TimeReceived = new DateTime(2025, 3, 28, 6, 53, 19),
            SIG = 0.9m,
            SRC = "HOT",
            ID = "6720",
            CMD = "SRQ",
            TYP = "NML",
            RR = "CN",
        };

        // Act
        var actual = HotEotDeserializer.Deserialize(data);

        // Assert
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Deserialize_Valid2_Test()
    {
        // Arrange
        var data = "2025/03/28-06:58:15  1.3 EOT  6720  88 1 1 OK   0 1 --- NML 1 0 CN   ------------";
        var expected = new HotEotPacket
        {
            TimeReceived = new DateTime(2025, 3, 28, 6, 58, 15),
            SIG = 1.3m,
            SRC = "EOT",
            ID = "6720",
            BP = 88,
            MOT = 1,
            MRK = 1,
            BATST = "OK",
            BATCU = 0,
            TRB = 1,
            TYP = "NML",
            VLV = 1,
            CNF = 0,
            RR = "CN"
        };

        // Act
        var actual = HotEotDeserializer.Deserialize(data);

        // Assert
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Deserialize_Valid3_Test()
    {
        // Arrange
        var data = "2025/03/28-06:58:15  1.3 EOT  6720  88 1 1 OK   0 1 --- NML 1 0 WSOR T4H         ";
        var expected = new HotEotPacket
        {
            TimeReceived = new DateTime(2025, 3, 28, 6, 58, 15),
            SIG = 1.3m,
            SRC = "EOT",
            ID = "6720",
            BP = 88,
            MOT = 1,
            MRK = 1,
            BATST = "OK",
            BATCU = 0,
            TRB = 1,
            TYP = "NML",
            VLV = 1,
            CNF = 0,
            RR = "WSOR",
            SYMB = "T4H"
        };

        // Act
        var actual = HotEotDeserializer.Deserialize(data);

        // Assert
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Deserialize_Invalid_Test()
    {
        // Arrange
        var data = "2025/03/28-09:04:33 ---- INV ----- --- - - -- --- - --- --- - - ---- ------------";
        var expected = new HotEotPacket
        {
            TimeReceived = new DateTime(2025, 3, 28, 9, 04, 33),
            SRC = "INV"
        };

        // Act
        var actual = HotEotDeserializer.Deserialize(data);

        // Assert
        Assert.AreEqual(expected, actual);
    }
}
