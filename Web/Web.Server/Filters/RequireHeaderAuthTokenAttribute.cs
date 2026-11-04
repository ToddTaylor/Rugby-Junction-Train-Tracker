using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Web.Server.Middleware;

namespace Web.Server.Filters
{
    /// <summary>
    /// Refuses the request with 401 when the user signed in with the access_token query string instead of a header.
    /// A link, an image tag or a crawler can carry a query string but cannot set a header, so actions that change
    /// a beacon PC (reboot, device keys) only accept the token from the Authorization or X-Auth-Token header.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class RequireHeaderAuthTokenAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.Items.TryGetValue(AuthTokenMiddleware.TokenFromQueryItemKey, out var fromQuery) && fromQuery is true)
            {
                context.Result = new ObjectResult(new { errors = new[] { "Send the sign-in token in a header for this action." } })
                {
                    StatusCode = StatusCodes.Status401Unauthorized,
                };
            }
        }
    }
}
