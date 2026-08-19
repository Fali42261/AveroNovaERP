using AveroNova.Application.Interfaces;
using AveroNova.Domain.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AveroNova.API.Filters
{
    public sealed class RequireActiveSubscriptionFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var controller = context.RouteData.Values["controller"]?.ToString() ?? string.Empty;
            var action = context.RouteData.Values["action"]?.ToString() ?? string.Empty;

            if (IsExempt(controller, action))
            {
                await next();
                return;
            }

            var access = context.HttpContext.RequestServices.GetRequiredService<IAccessControlService>();
            var subscriptions = context.HttpContext.RequestServices.GetRequiredService<ICompanySubscriptionService>();
            var userId = ParseGuid(context.HttpContext.Request, "X-User-Id");
            var companyId = ParseGuid(context.HttpContext.Request, "X-Company-Id");

            if (userId == Guid.Empty || companyId == Guid.Empty)
            {
                context.Result = Unauthorized(SubscriptionMessages.CompanyContextRequired);
                return;
            }

            if (!await access.UserBelongsToCompanyAsync(userId, companyId))
            {
                context.Result = Forbidden(SubscriptionMessages.UserNotInCompany);
                return;
            }

            var snapshot = await subscriptions.GetCurrentAsync(companyId);
            if (snapshot == null || snapshot.IsExpired || !snapshot.IsActive)
            {
                context.Result = Forbidden(SubscriptionMessages.FreeTrialExpiredAccess);
                return;
            }

            await next();
        }

        private static bool IsExempt(string controller, string action)
        {
            if (string.Equals(controller, "WeatherForecast", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.Equals(controller, "Subscriptions", StringComparison.OrdinalIgnoreCase))
                return false;

            return action is "GetCurrent" or "LoginAccess" or "Reminder";
        }

        private static ObjectResult Forbidden(string message) => new(new { message })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };

        private static ObjectResult Unauthorized(string message) => new(new { message })
        {
            StatusCode = StatusCodes.Status401Unauthorized
        };

        private static Guid ParseGuid(HttpRequest request, string header)
        {
            if (request.Headers.TryGetValue(header, out var values)
                && Guid.TryParse(values.FirstOrDefault(), out var id))
            {
                return id;
            }

            return Guid.Empty;
        }
    }
}
