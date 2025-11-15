using Web.Server.Entities;
using Web.Server.Enums;

namespace Web.Server.Data
{
    internal static class SeedData
    {
        internal static void SeedDatabase(TelemetryDbContext dbContext)
        {
            var staticDate = new DateTime(2025, 11, 1);

            // Seed Users
            if (!dbContext.Users.Any())
            {
                dbContext.Users.AddRange(
                    new User { ID = 1, FirstName = "Todd", LastName = "Taylor", Email = "rugbyjunctionwi@outlook.com", IsActive = true, CreatedAt = staticDate, LastUpdate = staticDate },
                    new User { ID = 2, FirstName = "Chris", LastName = "Stromberg", Email = "c.k.stromberg@gmail.com", IsActive = true, CreatedAt = staticDate, LastUpdate = staticDate },
                    new User { ID = 3, FirstName = "Thomas", LastName = "Hogan", Email = "TheSteelHighway@gmail.com", IsActive = false, CreatedAt = staticDate, LastUpdate = staticDate },
                    new User { ID = 4, FirstName = "Jesus", LastName = "Micheel", Email = "Jesusmicheel43@gmail.com", IsActive = true, CreatedAt = staticDate, LastUpdate = staticDate }
                );
            }

            // Seed Roles
            if (!dbContext.Roles.Any())
            {
                dbContext.Roles.AddRange(
                    new Role { RoleId = 1, RoleName = "Admin", Description = "Full access" },
                    new Role { RoleId = 2, RoleName = "Editor", Description = "Can edit content" },
                    new Role { RoleId = 3, RoleName = "Viewer", Description = "Read-only access" }
                );
            }

            // Seed Railroads
            if (!dbContext.Railroads.Any())
            {
                dbContext.Railroads.AddRange(
                    new Railroad { ID = 1, Name = "CN", CreatedAt = staticDate, LastUpdate = staticDate },
                    new Railroad { ID = 2, Name = "WSOR", CreatedAt = staticDate, LastUpdate = staticDate },
                    new Railroad { ID = 3, Name = "CPKC", CreatedAt = staticDate, LastUpdate = staticDate },
                    new Railroad { ID = 4, Name = "UP", CreatedAt = staticDate, LastUpdate = staticDate },
                    new Railroad { ID = 5, Name = "BNSF", CreatedAt = staticDate, LastUpdate = staticDate },
                    new Railroad { ID = 6, Name = "FOXY", CreatedAt = staticDate, LastUpdate = staticDate }
                );
            }

            // Seed Subdivisions
            if (!dbContext.Subdivisions.Any())
            {
                dbContext.Subdivisions.AddRange(
                    new Subdivision { ID = 1, RailroadID = 1, DpuCapable = true, Name = "Waukesha", CreatedAt = staticDate, LastUpdate = staticDate },
                    new Subdivision { ID = 2, RailroadID = 2, DpuCapable = false, Name = "Milwaukee", CreatedAt = staticDate, LastUpdate = staticDate },
                    new Subdivision { ID = 3, RailroadID = 1, DpuCapable = true, Name = "Neenah", CreatedAt = staticDate, LastUpdate = staticDate },
                    new Subdivision { ID = 4, RailroadID = 1, DpuCapable = true, Name = "Superior", CreatedAt = staticDate, LastUpdate = staticDate }
                );
            }

            // Seed Beacons
            if (!dbContext.Beacons.Any())
            {
                dbContext.Beacons.AddRange(
                    new Beacon { ID = 1, OwnerID = 1, Name = "Rugby Junction", CreatedAt = staticDate, LastUpdate = staticDate },
                    new Beacon { ID = 2, OwnerID = 2, Name = "Sussex", CreatedAt = staticDate, LastUpdate = staticDate },
                    new Beacon { ID = 4, OwnerID = 3, Name = "Ladysmith", CreatedAt = staticDate, LastUpdate = staticDate },
                    new Beacon { ID = 5, OwnerID = 3, Name = "Neenah", CreatedAt = staticDate, LastUpdate = staticDate },
                    new Beacon { ID = 6, OwnerID = 3, Name = "Stanberry", CreatedAt = staticDate, LastUpdate = staticDate },
                    new Beacon { ID = 7, OwnerID = 3, Name = "Owen", CreatedAt = staticDate, LastUpdate = staticDate },
                    new Beacon { ID = 8, OwnerID = 3, Name = "Gordon", CreatedAt = staticDate, LastUpdate = staticDate },
                    new Beacon { ID = 9, OwnerID = 3, Name = "Stone Lake", CreatedAt = staticDate, LastUpdate = staticDate },
                    new Beacon { ID = 10, OwnerID = 4, Name = "South Neenah", CreatedAt = staticDate, LastUpdate = staticDate }
                );
            }

            // Seed BeaconRailroads
            if (!dbContext.BeaconRailroads.Any())
            {
                dbContext.BeaconRailroads.AddRange(
                    new BeaconRailroad { BeaconID = 1, SubdivisionID = 1, Direction = Direction.NorthSouth, Latitude = 43.280958, Longitude = -88.214682, Milepost = 117.2, MultipleTracks = true, CreatedAt = staticDate, LastUpdate = staticDate },
                    new BeaconRailroad { BeaconID = 1, SubdivisionID = 2, Direction = Direction.NorthwestSoutheast, Latitude = 43.280958, Longitude = -88.213966, Milepost = 112.16, MultipleTracks = true, CreatedAt = staticDate, LastUpdate = staticDate },
                    new BeaconRailroad { BeaconID = 2, SubdivisionID = 1, Direction = Direction.NorthSouth, Latitude = 43.159517, Longitude = -88.200492, Milepost = 108.6, MultipleTracks = false, CreatedAt = staticDate, LastUpdate = staticDate },
                    new BeaconRailroad { BeaconID = 4, SubdivisionID = 4, Direction = Direction.All, Latitude = 45.463224, Longitude = -91.110779, Milepost = 129.0, MultipleTracks = true, CreatedAt = staticDate, LastUpdate = staticDate },
                    new BeaconRailroad { BeaconID = 5, SubdivisionID = 3, Direction = Direction.NorthSouth, Latitude = 44.171033, Longitude = -88.474169, Milepost = 185.3, MultipleTracks = true, CreatedAt = staticDate, LastUpdate = staticDate },
                    new BeaconRailroad { BeaconID = 6, SubdivisionID = 4, Direction = Direction.NorthSouth, Latitude = 45.993272, Longitude = -91.611853, Milepost = 401.2, MultipleTracks = true, CreatedAt = staticDate, LastUpdate = staticDate },
                    new BeaconRailroad { BeaconID = 7, SubdivisionID = 4, Direction = Direction.NorthSouth, Latitude = 44.947213, Longitude = -90.559989, Milepost = 308.0, MultipleTracks = false, CreatedAt = staticDate, LastUpdate = staticDate },
                    new BeaconRailroad { BeaconID = 8, SubdivisionID = 4, Direction = Direction.NorthSouth, Latitude = 46.244620, Longitude = -91.795016, Milepost = 421.0, MultipleTracks = false, CreatedAt = staticDate, LastUpdate = staticDate },
                    new BeaconRailroad { BeaconID = 9, SubdivisionID = 4, Direction = Direction.NorthSouth, Latitude = 45.854653, Longitude = -91.548329, Milepost = 389.0, MultipleTracks = true, CreatedAt = staticDate, LastUpdate = staticDate },
                    new BeaconRailroad { BeaconID = 10, SubdivisionID = 3, Direction = Direction.NorthSouth, Latitude = 44.164714, Longitude = -88.477814, Milepost = 185.0, MultipleTracks = true, CreatedAt = staticDate, LastUpdate = staticDate }
                );
            }

            // Seed UserRoles
            if (!dbContext.UserRoles.Any())
            {
                dbContext.UserRoles.AddRange(
                    new UserRole { UserId = 1, RoleId = 1, AssignedAt = staticDate },
                    new UserRole { UserId = 2, RoleId = 3, AssignedAt = staticDate },
                    new UserRole { UserId = 3, RoleId = 3, AssignedAt = staticDate },
                    new UserRole { UserId = 4, RoleId = 3, AssignedAt = staticDate }
                );
            }

            dbContext.SaveChanges();
        }
    }
}