using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Web.Server.DTOs;
using Web.Server.Entities;
using Web.Server.Services;

namespace Web.Server.Filters
{
    /// <summary>
    /// Refuses the request unless the signed-in user holds at least one of the given roles: 401 when nobody is
    /// signed in, 403 when the user holds none of the roles. The API key alone never passes, because the key ships
    /// in the web client and is public.
    /// </summary>
    /// <remarks>
    /// Runs as an authorization filter, before model binding, so a request that is not allowed never gets a
    /// validation error that describes the expected body. The user it loads is kept in HttpContext.Items under
    /// <see cref="CurrentUserItemKey"/> for actions that need to know who is asking.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class RequireRoleAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public const string CurrentUserItemKey = "CurrentUser";

        public RequireRoleAttribute(params string[] roles)
        {
            if (roles.Length == 0)
            {
                throw new ArgumentException("At least one role is required.", nameof(roles));
            }

            Roles = roles;
        }

        public IReadOnlyList<string> Roles { get; }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var httpContext = context.HttpContext;

            // AuthTokenMiddleware sets UserId only for a valid token of an active user.
            if (!httpContext.Items.TryGetValue("UserId", out var userIdObj) || userIdObj is not int userId)
            {
                context.Result = Refuse(StatusCodes.Status401Unauthorized, "Sign in to do this.");
                return;
            }

            var userService = httpContext.RequestServices.GetRequiredService<IUserService>();
            var user = await userService.GetUserByIdAsync(userId);

            if (user == null || !Roles.Any(role => ServerSupportUsers.HasRole(user, role)))
            {
                context.Result = Refuse(StatusCodes.Status403Forbidden, "You do not have permission to do this.");
                return;
            }

            httpContext.Items[CurrentUserItemKey] = user;
        }

        /// <summary>
        /// The user loaded by this filter for the current request, or null on an action without the filter.
        /// </summary>
        public static User? GetCurrentUser(HttpContext httpContext) =>
            httpContext.Items.TryGetValue(CurrentUserItemKey, out var value) ? value as User : null;

        private static ObjectResult Refuse(int statusCode, string error) =>
            new(new MessageEnvelope<object?>(null, [error])) { StatusCode = statusCode };
    }
}
