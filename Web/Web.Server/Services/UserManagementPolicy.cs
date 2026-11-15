using Web.Server.Entities;
using Web.Server.Enums;

namespace Web.Server.Services
{
    /// <summary>
    /// Who may create, change or delete which user. Admins may do anything. Custodians manage users too, but may
    /// not hand out the Admin role or touch a user who holds it, because either would let a Custodian become an
    /// Admin: directly, or by changing an Admin's email address to their own and signing in as that Admin.
    /// </summary>
    public static class UserManagementPolicy
    {
        public const string AssignAdminRefusal = "Only an Admin can assign the Admin role.";
        public const string ChangeAdminRefusal = "Only an Admin can change or delete an Admin user.";

        /// <summary>
        /// Returns why the change is refused, or null when it is allowed.
        /// </summary>
        /// <param name="actor">The signed-in user making the change.</param>
        /// <param name="target">The user as stored now, or null when a user is being created.</param>
        /// <param name="requestedRoles">The role names the user would hold afterwards; empty for a delete.</param>
        public static string? GetRefusal(User actor, User? target, IEnumerable<string> requestedRoles)
        {
            if (ServerSupportUsers.HasRole(actor, RoleNames.Admin))
            {
                return null;
            }

            if (target != null && ServerSupportUsers.HasRole(target, RoleNames.Admin))
            {
                return ChangeAdminRefusal;
            }

            if (requestedRoles.Any(role => string.Equals(role?.Trim(), RoleNames.Admin, StringComparison.OrdinalIgnoreCase)))
            {
                return AssignAdminRefusal;
            }

            return null;
        }
    }
}
