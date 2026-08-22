using AveroNova.API.Filters;
using AveroNova.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace AveroNova.API.Controllers
{
    [ApiController]
    [Route("api/modules")]
    public sealed class ModulesController : ControllerBase
    {
        [HttpGet("dashboard")]
        [RequireSubscriptionModule(SubscriptionModules.Dashboard)]
        public IActionResult Dashboard() => Ok(new { module = SubscriptionModules.Dashboard });

        [HttpGet("company")]
        [RequireSubscriptionModule(SubscriptionModules.Company)]
        public IActionResult Company() => Ok(new { module = SubscriptionModules.Company });

        [HttpGet("customers")]
        [RequireSubscriptionModule(SubscriptionModules.Customers)]
        public IActionResult Customers() => Ok(new { module = SubscriptionModules.Customers });

        [HttpGet("products")]
        [RequireSubscriptionModule(SubscriptionModules.Products)]
        public IActionResult Products() => Ok(new { module = SubscriptionModules.Products });

        [HttpGet("inventory")]
        [RequireSubscriptionModule(SubscriptionModules.Inventory)]
        public IActionResult Inventory() => Ok(new { module = SubscriptionModules.Inventory });

        [HttpGet("sales")]
        [RequireSubscriptionModule(SubscriptionModules.Sales)]
        public IActionResult Sales() => Ok(new { module = SubscriptionModules.Sales });

        [HttpGet("purchase")]
        [RequireSubscriptionModule(SubscriptionModules.Purchase)]
        public IActionResult Purchase() => Ok(new { module = SubscriptionModules.Purchase });

        [HttpGet("payments")]
        [RequireSubscriptionModule(SubscriptionModules.Payments)]
        public IActionResult Payments() => Ok(new { module = SubscriptionModules.Payments });

        [HttpGet("reports")]
        [RequireSubscriptionModule(SubscriptionModules.Reports)]
        public IActionResult Reports() => Ok(new { module = SubscriptionModules.Reports });

        [HttpGet("settings")]
        [RequireSubscriptionModule(SubscriptionModules.Settings)]
        public IActionResult Settings() => Ok(new { module = SubscriptionModules.Settings });

        [HttpGet("users-roles")]
        [RequireSubscriptionModule(SubscriptionModules.Settings, PermissionNames.UsersView)]
        public IActionResult UsersRoles() => Ok(new { module = SubscriptionModules.Settings, permission = PermissionNames.UsersView });
    }
}
