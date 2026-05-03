using MapsterMapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Hubs;
using Web.Server.Providers;
using Web.Server.Repositories;
using Web.Server.Services;

namespace Web.ServerTests.Services
{
    [TestClass]
    public class UserTrackedPinServiceTests
    {
        private readonly DateTime _nowUtc = new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc);
        private readonly Mock<IUserTrackedPinRepository> _trackedPinRepositoryMock = new();
        private readonly Mock<IMapPinRepository> _mapPinRepositoryMock = new();
        private readonly Mock<ITimeProvider> _timeProviderMock = new();
        private readonly Mock<IMapper> _mapperMock = new();
        private readonly Mock<ILogger<UserTrackedPinService>> _loggerMock = new();
        private readonly Mock<IHubContext<NotificationHub>> _hubContextMock = new();
        private readonly Mock<IHubClients> _hubClientsMock = new();
        private readonly Mock<IClientProxy> _clientProxyMock = new();

        private UserTrackedPinService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _timeProviderMock.Setup(t => t.UtcNow).Returns(_nowUtc);

            _hubClientsMock
                .Setup(c => c.Group(It.IsAny<string>()))
                .Returns(_clientProxyMock.Object);
            _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);

            _mapperMock
                .Setup(m => m.Map<UserTrackedPinDTO>(It.IsAny<UserTrackedPin>()))
                .Returns((UserTrackedPin src) => new UserTrackedPinDTO
                {
                    ID = src.ID,
                    UserId = src.UserId,
                    MapPinId = src.MapPinId,
                    BeaconID = src.BeaconID,
                    SubdivisionID = src.SubdivisionID,
                    Symbol = src.Symbol,
                    Color = src.Color,
                    ExpiresUtc = src.ExpiresUtc,
                    CreatedAt = src.CreatedAt,
                    LastUpdate = src.LastUpdate
                });

            _service = new UserTrackedPinService(
                _trackedPinRepositoryMock.Object,
                _mapPinRepositoryMock.Object,
                _timeProviderMock.Object,
                _mapperMock.Object,
                _loggerMock.Object,
                _hubContextMock.Object);
        }

        [TestMethod]
        public async Task AddByShareCodeAsync_NewTrackedPin_UsesExistingSymbolFromTrackedTrain()
        {
            const int userId = 12;
            const int mapPinId = 44;

            _mapPinRepositoryMock
                .Setup(r => r.GetByShareCodeAsync("ab12cd"))
                .ReturnsAsync(new MapPin
                {
                    ID = mapPinId,
                    BeaconID = 10,
                    SubdivisionId = 11,
                    ShareCode = "AB12CD"
                });

            _trackedPinRepositoryMock
                .Setup(r => r.GetByUserAndMapPinAsync(userId, mapPinId))
                .ReturnsAsync((UserTrackedPin?)null);

            _trackedPinRepositoryMock
                .Setup(r => r.GetByMapPinIdAsync(mapPinId))
                .ReturnsAsync(new List<UserTrackedPin>
                {
                    new UserTrackedPin { Symbol = "EXPIRED", ExpiresUtc = _nowUtc.AddMinutes(-5), LastUpdate = _nowUtc.AddMinutes(-1) },
                    new UserTrackedPin { Symbol = "TRAIN-9", ExpiresUtc = _nowUtc.AddHours(1), LastUpdate = _nowUtc.AddMinutes(-2) },
                    new UserTrackedPin { Symbol = "", ExpiresUtc = _nowUtc.AddHours(1), LastUpdate = _nowUtc }
                });

            _trackedPinRepositoryMock
                .Setup(r => r.GetByShareCodeAsync("AB12CD"))
                .ReturnsAsync(new List<UserTrackedPin>());

            _trackedPinRepositoryMock
                .Setup(r => r.GetByUserIdAsync(userId))
                .ReturnsAsync(new List<UserTrackedPin>());

            _trackedPinRepositoryMock
                .Setup(r => r.AddAsync(It.IsAny<UserTrackedPin>()))
                .ReturnsAsync((UserTrackedPin pin) =>
                {
                    pin.ID = 101;
                    return pin;
                });

            var result = await _service.AddByShareCodeAsync(userId, "ab12cd");

            Assert.AreEqual("TRAIN-9", result.Symbol);
            _trackedPinRepositoryMock.Verify(r => r.AddAsync(It.Is<UserTrackedPin>(pin =>
                pin.UserId == userId &&
                pin.MapPinId == mapPinId &&
                pin.Symbol == "TRAIN-9")), Times.Once);
        }

        [TestMethod]
        public async Task AddByShareCodeAsync_NewTrackedPin_FallsBackToShareCodeWhenNoSymbolExists()
        {
            const int userId = 22;
            const int mapPinId = 55;

            _mapPinRepositoryMock
                .Setup(r => r.GetByShareCodeAsync("ab12cd"))
                .ReturnsAsync(new MapPin
                {
                    ID = mapPinId,
                    BeaconID = 30,
                    SubdivisionId = 31,
                    ShareCode = "AB12CD"
                });

            _trackedPinRepositoryMock
                .Setup(r => r.GetByUserAndMapPinAsync(userId, mapPinId))
                .ReturnsAsync((UserTrackedPin?)null);

            _trackedPinRepositoryMock
                .Setup(r => r.GetByMapPinIdAsync(mapPinId))
                .ReturnsAsync(new List<UserTrackedPin>
                {
                    new UserTrackedPin { Symbol = null, ExpiresUtc = _nowUtc.AddHours(2), LastUpdate = _nowUtc },
                    new UserTrackedPin { Symbol = " ", ExpiresUtc = _nowUtc.AddHours(2), LastUpdate = _nowUtc.AddMinutes(-1) }
                });

            _trackedPinRepositoryMock
                .Setup(r => r.GetByShareCodeAsync("AB12CD"))
                .ReturnsAsync(new List<UserTrackedPin>());

            _trackedPinRepositoryMock
                .Setup(r => r.GetByUserIdAsync(userId))
                .ReturnsAsync(new List<UserTrackedPin>());

            _trackedPinRepositoryMock
                .Setup(r => r.AddAsync(It.IsAny<UserTrackedPin>()))
                .ReturnsAsync((UserTrackedPin pin) =>
                {
                    pin.ID = 202;
                    return pin;
                });

            var result = await _service.AddByShareCodeAsync(userId, "ab12cd");

            Assert.AreEqual("AB12CD", result.Symbol);
            _trackedPinRepositoryMock.Verify(r => r.AddAsync(It.Is<UserTrackedPin>(pin =>
                pin.UserId == userId &&
                pin.MapPinId == mapPinId &&
                pin.Symbol == "AB12CD")), Times.Once);
        }

        [TestMethod]
        public async Task AddByShareCodeAsync_NewTrackedPin_UsesSymbolFromShareCodeFallback()
        {
            const int userId = 30;
            const int mapPinId = 77;

            _mapPinRepositoryMock
                .Setup(r => r.GetByShareCodeAsync("ab12cd"))
                .ReturnsAsync(new MapPin
                {
                    ID = mapPinId,
                    BeaconID = 40,
                    SubdivisionId = 41,
                    ShareCode = "AB12CD"
                });

            _trackedPinRepositoryMock
                .Setup(r => r.GetByUserAndMapPinAsync(userId, mapPinId))
                .ReturnsAsync((UserTrackedPin?)null);

            _trackedPinRepositoryMock
                .Setup(r => r.GetByMapPinIdAsync(mapPinId))
                .ReturnsAsync(new List<UserTrackedPin>());

            _trackedPinRepositoryMock
                .Setup(r => r.GetByShareCodeAsync("AB12CD"))
                .ReturnsAsync(new List<UserTrackedPin>
                {
                    new UserTrackedPin { Symbol = "MY002", ExpiresUtc = _nowUtc.AddHours(1), LastUpdate = _nowUtc.AddMinutes(-1) },
                    new UserTrackedPin { Symbol = "OLD", ExpiresUtc = _nowUtc.AddMinutes(-1), LastUpdate = _nowUtc }
                });

            _trackedPinRepositoryMock
                .Setup(r => r.GetByUserIdAsync(userId))
                .ReturnsAsync(new List<UserTrackedPin>());

            _trackedPinRepositoryMock
                .Setup(r => r.AddAsync(It.IsAny<UserTrackedPin>()))
                .ReturnsAsync((UserTrackedPin pin) =>
                {
                    pin.ID = 303;
                    return pin;
                });

            var result = await _service.AddByShareCodeAsync(userId, "ab12cd");

            Assert.AreEqual("MY002", result.Symbol);
            _trackedPinRepositoryMock.Verify(r => r.GetByShareCodeAsync("AB12CD"), Times.Once);
            _trackedPinRepositoryMock.Verify(r => r.AddAsync(It.Is<UserTrackedPin>(pin =>
                pin.UserId == userId &&
                pin.MapPinId == mapPinId &&
                pin.Symbol == "MY002")), Times.Once);
        }
    }
}