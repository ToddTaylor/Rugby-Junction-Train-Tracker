using Microsoft.AspNetCore.SignalR;

namespace Web.Server.Hubs
{
    public class NotificationHub : Hub
    {
        private static string GetUserGroup(int userId) => $"user-{userId}";

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            if (userId.HasValue)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroup(userId.Value));
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserId();
            if (userId.HasValue)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetUserGroup(userId.Value));
            }

            await base.OnDisconnectedAsync(exception);
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
