using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Web.Server.Controllers.v1;
using Web.Server.Filters;

namespace Web.ServerTests.Controllers;

/// <summary>
/// Which roles each admin endpoint asks for. The X-Api-Key header is public, so an action that changes data
/// and asks for nothing else is open to anyone.
/// </summary>
[TestClass]
public class EndpointAuthorizationTests
{
    private const string Admin = "Admin";
    private const string AdminOrCustodian = "Admin,Custodian";

    private const string BeaconPc = "Called by beacon PCs, which have no user; BeaconsController checks the device key.";
    private const string ServerHealth = "The action checks for an Admin or a Custodian of that beacon.";
    private const string OwnPins = "Works only on the pins of the signed-in user.";

    /// <summary>
    /// Actions that change data without RequireRole, and what protects each instead.
    /// </summary>
    private static readonly Dictionary<string, string> ProtectedAnotherWay = new()
    {
        ["AuthController.Logout"] = "Needs the token it invalidates.",
        ["AuthController.SendCode"] = "Sign-in starts here; mails a code to an active user only, rate limited, same answer for every address.",
        ["AuthController.VerifyCode"] = "Sign-in ends here; needs the emailed code, which is discarded after five wrong tries.",
        ["TelemetriesController.PostTelemetry"] = "Called by beacon PCs, which have no user.",
        ["BeaconsController.CheckHealth"] = BeaconPc,
        ["BeaconsController.ReportCommand"] = BeaconPc,
        ["SubdivisionsController.PutSubdivision"] = "The action checks for an Admin, or the Custodian of that subdivision changing local train addresses.",
        ["ServerHealthController.UpdateExpectations"] = ServerHealth,
        ["ServerHealthController.MuteAlerts"] = ServerHealth,
        ["ServerHealthController.UnmuteAlerts"] = ServerHealth,
        ["ServerHealthController.CreateDeviceKey"] = ServerHealth,
        ["ServerHealthController.RemoveDeviceKey"] = ServerHealth,
        ["ServerHealthController.RequestCommand"] = ServerHealth,
        ["ServerHealthController.CancelCommand"] = ServerHealth,
        ["UserTrackedPinsController.UpdateTrackedPinSymbol"] = OwnPins,
        ["UserTrackedPinsController.UpdateTrackedPinLocation"] = OwnPins,
        ["UserTrackedPinsController.RemoveTrackedPin"] = OwnPins,
        ["UserTrackedPinsController.AddTrackedPin"] = OwnPins,
        ["UserTrackedPinsController.AddTrackedPinByShareCode"] = OwnPins,
    };

    [TestMethod]
    // Users: the whole controller, reads included, because it lists the email address of every user.
    [DataRow(typeof(UsersController), nameof(UsersController.GetUsers), AdminOrCustodian)]
    [DataRow(typeof(UsersController), nameof(UsersController.GetUser), AdminOrCustodian)]
    [DataRow(typeof(UsersController), nameof(UsersController.GetUserByEmail), AdminOrCustodian)]
    [DataRow(typeof(UsersController), nameof(UsersController.PostUser), AdminOrCustodian)]
    [DataRow(typeof(UsersController), nameof(UsersController.PutUser), AdminOrCustodian)]
    [DataRow(typeof(UsersController), nameof(UsersController.DeleteUser), AdminOrCustodian)]
    [DataRow(typeof(RailroadsController), nameof(RailroadsController.PostRailroad), AdminOrCustodian)]
    [DataRow(typeof(RailroadsController), nameof(RailroadsController.PutRailroad), AdminOrCustodian)]
    [DataRow(typeof(RailroadsController), nameof(RailroadsController.DeleteRailroad), AdminOrCustodian)]
    [DataRow(typeof(SubdivisionTrackageRightsController), nameof(SubdivisionTrackageRightsController.ReplaceTrackageRights), AdminOrCustodian)]
    [DataRow(typeof(SubdivisionTrackageRightsController), nameof(SubdivisionTrackageRightsController.DeleteTrackageRight), AdminOrCustodian)]
    [DataRow(typeof(MapPinsController), nameof(MapPinsController.DeleteBeacon), AdminOrCustodian)]
    [DataRow(typeof(MapPinsController), nameof(MapPinsController.ClearAddresses), Admin)]
    [DataRow(typeof(BeaconsController), nameof(BeaconsController.PostBeacon), Admin)]
    [DataRow(typeof(BeaconsController), nameof(BeaconsController.PutBeacon), Admin)]
    [DataRow(typeof(BeaconsController), nameof(BeaconsController.DeleteBeacon), Admin)]
    [DataRow(typeof(BeaconRailroadsController), nameof(BeaconRailroadsController.Create), Admin)]
    [DataRow(typeof(BeaconRailroadsController), nameof(BeaconRailroadsController.Update), Admin)]
    [DataRow(typeof(BeaconRailroadsController), nameof(BeaconRailroadsController.Delete), Admin)]
    [DataRow(typeof(SubdivisionsController), nameof(SubdivisionsController.PostSubdivision), Admin)]
    [DataRow(typeof(SubdivisionsController), nameof(SubdivisionsController.DeleteSubdivision), Admin)]
    [DataRow(typeof(AmtrakController), nameof(AmtrakController.GetTrackedTrains), Admin)]
    [DataRow(typeof(AmtrakController), nameof(AmtrakController.AddTrackedTrain), Admin)]
    [DataRow(typeof(AmtrakController), nameof(AmtrakController.DeleteTrackedTrain), Admin)]
    [DataRow(typeof(AmtrakController), nameof(AmtrakController.GetPollingConfiguration), Admin)]
    [DataRow(typeof(AmtrakController), nameof(AmtrakController.UpdatePollingConfiguration), Admin)]
    [DataRow(typeof(LogsController), nameof(LogsController.GetLatestLogs), Admin)]
    [DataRow(typeof(LogsController), nameof(LogsController.GetLogFiles), Admin)]
    [DataRow(typeof(LogsController), nameof(LogsController.GetLogFile), Admin)]
    [DataRow(typeof(LogsController), nameof(LogsController.SearchLogs), Admin)]
    public void Action_RequiresRoles(Type controller, string action, string expectedRoles)
    {
        var method = controller.GetMethod(action);
        Assert.IsNotNull(method, $"{controller.Name}.{action} not found.");

        var required = GetRequiredRoles(method);

        Assert.AreEqual(1, required.Count, $"{controller.Name}.{action} should carry exactly one RequireRole.");
        CollectionAssert.AreEquivalent(expectedRoles.Split(','), required[0].ToList());
    }

    [TestMethod]
    public void EveryActionThatChangesData_RequiresARole_OrIsListedAsProtectedAnotherWay()
    {
        var changingActions = GetActions()
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>()
                .Any(a => a.HttpMethods.Any(verb => !HttpMethods.IsGet(verb) && !HttpMethods.IsHead(verb))))
            .ToList();

        Assert.IsTrue(changingActions.Count > 0);

        var unprotected = changingActions
            .Where(m => GetRequiredRoles(m).Count == 0)
            .Select(Name)
            .Where(name => !ProtectedAnotherWay.ContainsKey(name))
            .ToList();

        Assert.AreEqual(0, unprotected.Count,
            "Add [RequireRole] or list why it is safe in ProtectedAnotherWay: " + string.Join(", ", unprotected));
    }

    [TestMethod]
    public void ProtectedAnotherWay_ListsOnlyActionsThatExistWithoutRequireRole()
    {
        var actionsWithoutRole = GetActions().Where(m => GetRequiredRoles(m).Count == 0).Select(Name).ToHashSet();

        var stale = ProtectedAnotherWay.Keys.Where(name => !actionsWithoutRole.Contains(name)).ToList();

        Assert.AreEqual(0, stale.Count, "Remove from ProtectedAnotherWay: " + string.Join(", ", stale));
    }

    private static IEnumerable<MethodInfo> GetActions() =>
        typeof(UsersController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));

    private static string Name(MethodInfo method) => $"{method.DeclaringType!.Name}.{method.Name}";

    private static List<IReadOnlyList<string>> GetRequiredRoles(MethodInfo method) =>
        method.GetCustomAttributes<RequireRoleAttribute>()
            .Concat(method.DeclaringType!.GetCustomAttributes<RequireRoleAttribute>())
            .Select(a => a.Roles)
            .ToList();
}
