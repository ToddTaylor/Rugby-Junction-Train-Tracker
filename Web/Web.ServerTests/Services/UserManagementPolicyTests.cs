using Web.Server.Entities;
using Web.Server.Services;

namespace Web.ServerTests.Services;

/// <summary>
/// Custodians manage users, but must not be able to turn themselves into Admins.
/// </summary>
[TestClass]
public class UserManagementPolicyTests
{
    [TestMethod]
    public void Admin_MayAssignAdmin_AndChangeAnAdmin()
    {
        var admin = UserWith("Admin");

        Assert.IsNull(UserManagementPolicy.GetRefusal(admin, target: null, ["Admin"]));
        Assert.IsNull(UserManagementPolicy.GetRefusal(admin, UserWith("Admin"), ["Custodian"]));
        Assert.IsNull(UserManagementPolicy.GetRefusal(admin, UserWith("Admin"), []));
    }

    [TestMethod]
    [DataRow("Admin")]
    [DataRow("admin")]
    [DataRow(" Admin ")]
    public void Custodian_MayNotAssignAdmin(string requestedRole)
    {
        var custodian = UserWith("Custodian");

        Assert.AreEqual(UserManagementPolicy.AssignAdminRefusal, UserManagementPolicy.GetRefusal(custodian, target: null, [requestedRole]));
        Assert.AreEqual(UserManagementPolicy.AssignAdminRefusal, UserManagementPolicy.GetRefusal(custodian, UserWith("Custodian"), ["Custodian", requestedRole]));
    }

    [TestMethod]
    public void Custodian_MayNotChangeOrDeleteAnAdmin()
    {
        var custodian = UserWith("Custodian");

        // Keeping the roles as they are still counts: the email address could be changed to take over the account.
        Assert.AreEqual(UserManagementPolicy.ChangeAdminRefusal, UserManagementPolicy.GetRefusal(custodian, UserWith("Admin"), ["Admin"]));
        Assert.AreEqual(UserManagementPolicy.ChangeAdminRefusal, UserManagementPolicy.GetRefusal(custodian, UserWith("Admin"), []));
    }

    [TestMethod]
    public void Custodian_MayManageOtherUsers()
    {
        var custodian = UserWith("Custodian");

        Assert.IsNull(UserManagementPolicy.GetRefusal(custodian, target: null, []));
        Assert.IsNull(UserManagementPolicy.GetRefusal(custodian, target: null, ["Custodian"]));
        Assert.IsNull(UserManagementPolicy.GetRefusal(custodian, UserWith("Custodian"), []));
        Assert.IsNull(UserManagementPolicy.GetRefusal(custodian, UserWith(), ["Custodian"]));
    }

    private static User UserWith(params string[] roleNames) => new()
    {
        ID = 1,
        IsActive = true,
        UserRoles = roleNames
            .Select(name => new UserRole { Role = new Role { RoleName = name }, User = null!, AssignedAt = DateTime.UtcNow })
            .ToList(),
    };
}
