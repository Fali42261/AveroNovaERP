using AveroNova.Application.Interfaces;
using AveroNova.Domain.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AveroNova.API.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class RequireSubscriptionModuleAttribute : Attribute, IAsyncActionFilter
    {
        private readonly string _moduleKey;
        private readonly string? _permissionName;

        public RequireSubscriptionModuleAttribute(string moduleKey)
            : this(moduleKey, null)
        {
        }

        public RequireSubscriptionModuleAttribute(string moduleKey, string? permissionName)
        {
            _moduleKey = moduleKey;
            _permissionName = permissionName;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var access = context.HttpContext.RequestServices.GetRequiredService<IAccessControlService>();
            var userId = ParseGuid(context.HttpContext.Request, "X-User-Id");
            var companyId = ParseGuid(context.HttpContext.Request, "X-Company-Id");

            var decision = string.IsNullOrWhiteSpace(_permissionName)
                ? await access.AuthorizeAsync(userId, companyId, _moduleKey)
                : await access.AuthorizeFeatureAsync(userId, companyId, _moduleKey, _permissionName);
            if (!decision.IsAllowed)
            {
                context.Result = new ObjectResult(new
                {
                    message = decision.Reason ?? SubscriptionMessages.FreeTrialExpiredAccess,
                    module = _moduleKey,
                    expired = decision.IsSubscriptionExpired
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            await next();
        }

        private static Guid ParseGuid(HttpRequest request, string header)
        {
            if (request.Headers.TryGetValue(header, out var values)
                && Guid.TryParse(values.FirstOrDefault(), out var id))
            {
                return id;
            }

            if (request.Query.TryGetValue(header.Replace("X-", string.Empty).Replace("-", string.Empty), out var query)
                && Guid.TryParse(query.FirstOrDefault(), out var queryId))
            {
                return queryId;
            }

            return Guid.Empty;
        }
    }
}
