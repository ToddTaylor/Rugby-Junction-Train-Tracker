namespace Web.Server.Enums
{
    /// <summary>
    /// Role names as stored in Role.RoleName. A Custodian is an admin whose access to beacon PCs is limited to
    /// the subdivisions they are custodian of.
    /// </summary>
    public static class RoleNames
    {
        public const string Admin = "Admin";
        public const string Custodian = "Custodian";
    }
}
