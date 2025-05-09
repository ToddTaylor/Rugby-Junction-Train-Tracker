using Web.Server.Entities;
using Web.Server.Enums;
using Web.Server.Services;

namespace Web.ServerTests.Services
{
    [TestClass]
    public class DirectionServiceTests
    {
        [TestMethod]
        public void GetDirection_SameCoordinates_ReturnsEmptyString()
        {
            // Arrange
            var from = new GeoCoordinate(10.0, 20.0);
            var to = new GeoCoordinate(10.0, 20.0);

            // Act
            var result = DirectionService.GetDirection(from, to, Direction.All);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void GetDirection_AllDirections_ReturnsCorrectDirection()
        {
            // Arrange
            var from = new GeoCoordinate(10.0, 20.0);
            var to = new GeoCoordinate(15.0, 25.0);

            // Act
            var result = DirectionService.GetDirection(from, to, Direction.All);

            // Assert
            Assert.AreEqual("NE", result);
        }

        [TestMethod]
        public void GetDirection_NorthSouth_ReturnsCorrectDirection()
        {
            // Arrange
            var from = new GeoCoordinate(10.0, 20.0);
            var to = new GeoCoordinate(15.0, 20.0);

            // Act
            var result = DirectionService.GetDirection(from, to, Direction.NorthSouth);

            // Assert
            Assert.AreEqual("N", result);
        }

        [TestMethod]
        public void GetDirection_EastWest_ReturnsCorrectDirection()
        {
            // Arrange
            var from = new GeoCoordinate(10.0, 20.0);
            var to = new GeoCoordinate(10.0, 25.0);

            // Act
            var result = DirectionService.GetDirection(from, to, Direction.EastWest);

            // Assert
            Assert.AreEqual("E", result);
        }

        [TestMethod]
        public void GetDirection_NortheastSouthwest_ReturnsCorrectDirection()
        {
            // Arrange
            var from = new GeoCoordinate(10.0, 20.0);
            var to = new GeoCoordinate(15.0, 25.0);

            // Act
            var result = DirectionService.GetDirection(from, to, Direction.NortheastSouthwest);

            // Assert
            Assert.AreEqual("NE", result);
        }

        [TestMethod]
        public void GetDirection_NorthwestSoutheast_ReturnsCorrectDirection()
        {
            // Arrange
            var from = new GeoCoordinate(10.0, 20.0);
            var to = new GeoCoordinate(15.0, 15.0);

            // Act
            var result = DirectionService.GetDirection(from, to, Direction.NorthwestSoutheast);

            // Assert
            Assert.AreEqual("NW", result);
        }
    }
}
