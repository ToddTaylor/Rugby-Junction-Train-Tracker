using Web.Server.Entities;

namespace Web.Server.Services
{
    /// <summary>
    /// Who may see and manage a beacon PC on the Server Health page: every Admin, and the Custodians of the
    /// subdivisions the beacon serves.
    /// </summary>
    public static class ServerSupportUsers
    {
        public static bool HasRole(User user, string roleName) =>
            user.UserRoles?.Any(ur => string.Equals(ur.Role?.RoleName, roleName, StringComparison.OrdinalIgnoreCase)) == true;

        public static bool IsCustodianOf(User user, Beacon beacon) =>
            beacon.BeaconRailroads.Any(br => br.Subdivision?.CustodianId == user.ID);

        /// <summary>
        /// IDs of the active Admins and of the beacon's active Custodians, without duplicates.
        /// </summary>
        public static IReadOnlyList<int> GetRecipientIds(Beacon beacon, IEnumerable<User> users)
        {
            var activeUsers = users.Where(u => u.IsActive).ToList();
            var custodianIds = activeUsers.Where(u => HasRole(u, "Custodian")).Select(u => u.ID).ToHashSet();

            return activeUsers
                .Where(u => HasRole(u, "Admin"))
                .Select(u => u.ID)
                .Concat(beacon.BeaconRailroads
                    .Select(br => br.Subdivision?.CustodianId)
                    .OfType<int>()
                    .Where(custodianIds.Contains))
                .Distinct()
                .ToList();
        }

        public static string DisplayName(User user)
        {
            var name = $"{user.FirstName} {user.LastName}".Trim();
            return name.Length > 0 ? name : string.IsNullOrWhiteSpace(user.Email) ? $"User {user.ID}" : user.Email;
        }
    }
}
