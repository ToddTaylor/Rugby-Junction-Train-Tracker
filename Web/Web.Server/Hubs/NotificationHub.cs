using Microsoft.AspNetCore.SignalR;
using Web.Server.Services;

namespace Web.Server.Hubs
{
    public class NotificationHub : Hub
    {
        public const string SupportUsersGroup = "support-users";
        public const string ViewerUsersGroup = "viewer-users";

        private readonly IUserService _userService;

        public NotificationHub(IUserService userService)
        {
            _userService = userService;
        }

        private static string GetUserGroup(int userId) => $"user-{userId}";

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            if (userId.HasValue)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroup(userId.Value));
                var roleGroup = await GetRoleGroupAsync(userId.Value);
                await Groups.AddToGroupAsync(Context.ConnectionId, roleGroup);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserId();
            if (userId.HasValue)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetUserGroup(userId.Value));
                var roleGroup = await GetRoleGroupAsync(userId.Value);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roleGroup);
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task<string> GetRoleGroupAsync(int userId)
        {
            var user = await _userService.GetUserByIdAsync(userId);
            var isSupportUser = user?.UserRoles?.Any(ur =>
                string.Equals(ur.Role?.RoleName, "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ur.Role?.RoleName, "Custodian", StringComparison.OrdinalIgnoreCase)) == true;
            return isSupportUser ? SupportUsersGroup : ViewerUsersGroup;
        }

        private int? GetUserId()
        {
            var httpContext = Context.GetHttpContext();
            if (httpContext?.Items.TryGetValue("UserId", out var value) == true && value is int userId)
            {
                return userId;
            }
            return null;
        }
    }
}
