using System.ComponentModel.DataAnnotations;
using Web.Server.DTOs;

namespace Web.ServerTests.DTOs;

/// <summary>
/// Train symbols are typed by users and shown to other users, so the server limits their length rather than
/// trusting the web client to.
/// </summary>
[TestClass]
public class TrackedPinRequestDTOTests
{
    [TestMethod]
    [DataRow(null, true)]
    [DataRow("", true)]
    [DataRow("M342", true)]
    [DataRow("ABCDEFGHIJ", true)]
    [DataRow("ABCDEFGHIJK", false)]
    [DataRow("<img src=x onerror=alert(1)>", false)]
    public void Symbol_IsLimitedToTenCharacters(string? symbol, bool expectedValid)
    {
        Assert.AreEqual(expectedValid, IsValid(new UpdateTrackedPinSymbolRequestDTO { Symbol = symbol }));
        Assert.AreEqual(expectedValid, IsValid(new AddTrackedPinRequestDTO { MapPinId = 1, Symbol = symbol }));
    }

    private static bool IsValid(object dto) =>
        Validator.TryValidateObject(dto, new ValidationContext(dto), new List<ValidationResult>(), validateAllProperties: true);
}
