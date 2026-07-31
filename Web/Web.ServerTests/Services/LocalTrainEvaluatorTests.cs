using System.Diagnostics.CodeAnalysis;
using Web.Server.Entities;
using Web.Server.Services;

namespace Web.ServerTests.Services
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public class LocalTrainEvaluatorTests
    {
        private static Subdivision MakeSubdivision(string? localTrainAddressIDs)
        {
            return new Subdivision
            {
                ID = 1,
                RailroadID = 1,
                Railroad = new Railroad { ID = 1, Name = "CN" },
                Name = "Waukesha",
                LocalTrainAddressIDs = localTrainAddressIDs,
            };
        }

        [TestMethod]
        public void IsLocalTrain_ReturnsFalse_WhenSubdivisionIsNull()
        {
            Assert.IsFalse(LocalTrainEvaluator.IsLocalTrain(1, null));
        }

        [TestMethod]
        public void IsLocalTrain_ReturnsFalse_WhenLocalTrainAddressIDsIsNullOrEmpty()
        {
            Assert.IsFalse(LocalTrainEvaluator.IsLocalTrain(1, MakeSubdivision(null)));
            Assert.IsFalse(LocalTrainEvaluator.IsLocalTrain(1, MakeSubdivision("")));
            Assert.IsFalse(LocalTrainEvaluator.IsLocalTrain(1, MakeSubdivision("   ")));
        }

        [TestMethod]
        public void IsLocalTrain_ReturnsTrue_WhenAddressIsInCommaSeparatedList()
        {
            Assert.IsTrue(LocalTrainEvaluator.IsLocalTrain(29353, MakeSubdivision("29352,29353,29354")));
        }

        [TestMethod]
        public void IsLocalTrain_ReturnsTrue_WhenAddressIsInNewlineSeparatedList()
        {
            Assert.IsTrue(LocalTrainEvaluator.IsLocalTrain(29353, MakeSubdivision("29352\n29353\n29354")));
        }

        [TestMethod]
        public void IsLocalTrain_ReturnsFalse_WhenAddressIsNotInList()
        {
            Assert.IsFalse(LocalTrainEvaluator.IsLocalTrain(99999, MakeSubdivision("29352,29353")));
        }

        [TestMethod]
        public void IsLocalTrain_IgnoresNonNumericEntries()
        {
            Assert.IsTrue(LocalTrainEvaluator.IsLocalTrain(29353, MakeSubdivision("abc,29353,")));
        }
    }
}
