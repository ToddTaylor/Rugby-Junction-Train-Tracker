using Mapster;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Mappers;

namespace Web.ServerTests.Mappers
{
    [ExcludeFromCodeCoverage]
    [TestClass]
    public class MapsterProfileTests
    {
        [TestMethod]
        public void MapPinMapping_SetsOnlyNewestAddressAsActive()
        {
            var config = new TypeAdapterConfig();
            config.Scan(typeof(MapsterProfile).Assembly);

            var older = new DateTime(2026, 4, 18, 10, 0, 0, DateTimeKind.Utc);
            var newer = older.AddMinutes(1);

            var mapPin = new MapPin
            {
                ID = 42,
                BeaconID = 1,
                Direction = "N",
                CreatedAt = older,
                LastUpdate = newer,
                Moving = true,
                Addresses =
                [
                    new Address
                    {
                        AddressID = 31188,
                        Source = "HOT",
                        CreatedAt = older,
                        LastUpdate = older
                    },
                    new Address
                    {
                        AddressID = 31188,
                        Source = "EOT",
                        CreatedAt = newer,
                        LastUpdate = newer
                    }
                ],
                BeaconRailroad = new BeaconRailroad
                {
                    BeaconID = 1,
                    Latitude = 44.0,
                    Longitude = -88.0,
                    Milepost = 117.2,
                    Beacon = new Beacon { Name = "Rugby Jct" },
                    Subdivision = new Subdivision
                    {
                        ID = 99,
                        Name = "Waukesha",
                        Railroad = new Railroad { Name = "CN" }
                    }
                }
            };

            var dto = mapPin.Adapt<MapPinDTO>(config);

            Assert.AreEqual(2, dto.Addresses.Count);
            Assert.IsTrue(dto.Addresses.Single(a => a.Source == "EOT").IsActive);
            Assert.IsFalse(dto.Addresses.Single(a => a.Source == "HOT").IsActive);
        }

        [TestMethod]
        public void MapPinMapping_IncludesDpuAndSetsDpuActiveWhenNewest()
        {
            var config = new TypeAdapterConfig();
            config.Scan(typeof(MapsterProfile).Assembly);

            var t0 = new DateTime(2026, 4, 18, 10, 0, 0, DateTimeKind.Utc);
            var t1 = t0.AddMinutes(1);
            var t2 = t1.AddMinutes(1);

            var mapPin = new MapPin
            {
                ID = 84,
                BeaconID = 1,
                Direction = "N",
                CreatedAt = t0,
                LastUpdate = t2,
                Moving = true,
                Addresses =
                [
                    new Address
                    {
                        AddressID = 31188,
                        Source = "HOT",
                        CreatedAt = t0,
                        LastUpdate = t0
                    },
                    new Address
                    {
                        AddressID = 31188,
                        Source = "EOT",
                        CreatedAt = t1,
                        LastUpdate = t1
                    },
                    new Address
                    {
                        AddressID = 77701,
                        DpuTrainID = 5401,
                        Source = "DPU",
                        CreatedAt = t2,
                        LastUpdate = t2
                    }
                ],
                BeaconRailroad = new BeaconRailroad
                {
                    BeaconID = 1,
                    Latitude = 44.0,
                    Longitude = -88.0,
                    Milepost = 117.2,
                    Beacon = new Beacon { Name = "Rugby Jct" },
                    Subdivision = new Subdivision
                    {
                        ID = 99,
                        Name = "Waukesha",
                        Railroad = new Railroad { Name = "CN" }
                    }
                }
            };

            var dto = mapPin.Adapt<MapPinDTO>(config);

            Assert.AreEqual(3, dto.Addresses.Count);
            Assert.IsTrue(dto.Addresses.Single(a => a.Source == "DPU").IsActive);
            Assert.IsFalse(dto.Addresses.Single(a => a.Source == "HOT").IsActive);
            Assert.IsFalse(dto.Addresses.Single(a => a.Source == "EOT").IsActive);
        }

        [TestMethod]
        public void MapPinMapping_MapsShareCode()
        {
            var config = new TypeAdapterConfig();
            config.Scan(typeof(MapsterProfile).Assembly);

            var now = new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc);

            var mapPin = new MapPin
            {
                ID = 11,
                BeaconID = 1,
                ShareCode = "ZX82QP",
                Direction = "S",
                CreatedAt = now,
                LastUpdate = now,
                Moving = true,
                Addresses = [],
                BeaconRailroad = new BeaconRailroad
                {
                    BeaconID = 1,
                    Latitude = 44.0,
                    Longitude = -88.0,
                    Milepost = 117.2,
                    Beacon = new Beacon { Name = "Rugby Jct" },
                    Subdivision = new Subdivision
                    {
                        ID = 99,
                        Name = "Waukesha",
                        Railroad = new Railroad { Name = "CN" }
                    }
                }
            };

            var dto = mapPin.Adapt<MapPinDTO>(config);

            Assert.AreEqual("ZX82QP", dto.ShareCode);
        }
    }
}
