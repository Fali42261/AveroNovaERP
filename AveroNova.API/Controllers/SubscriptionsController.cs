using AveroNova.Application.DTOs;
using AveroNova.Application.Interfaces;
using AveroNova.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace AveroNova.API.Controllers
{
    [ApiController]
    [Route("api/subscriptions")]
    public sealed class SubscriptionsController : ControllerBase
    {
        private readonly ICompanySubscriptionService _subscriptions;
        private readonly IAccessControlService _access;

        public SubscriptionsController(
            ICompanySubscriptionService subscriptions,
            IAccessControlService access)
        {
            _subscriptions = subscriptions;
            _access = access;
        }

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrent(
            [FromHeader(Name = "X-User-Id")] Guid userId,
            [FromHeader(Name = "X-Company-Id")] Guid companyId,
            CancellationToken cancellationToken)
        {
            if (companyId == Guid.Empty)
                return BadRequest(new { message = "X-Company-Id is required." });

            var membership = await EnsureMembershipAsync(userId, companyId, cancellationToken);
            if (membership != null)
                return membership;

            var snapshot = await _subscriptions.GetCurrentAsync(companyId, cancellationToken);
            if (snapshot == null)
                return NotFound(new { message = "No subscription found for this company." });

            return Ok(snapshot);
        }

        [HttpGet("login-access")]
        public async Task<IActionResult> LoginAccess(
            [FromHeader(Name = "X-User-Id")] Guid userId,
            [FromHeader(Name = "X-Company-Id")] Guid companyId,
            CancellationToken cancellationToken)
        {
            if (userId == Guid.Empty)
                return BadRequest(new { message = "X-User-Id is required." });

            var result = await _subscriptions.ResolveLoginCompanyAsync(
                userId,
                companyId == Guid.Empty ? null : companyId,
                cancellationToken);

            if (!result.IsAllowed)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = result.Message ?? SubscriptionMessages.FreeTrialExpiredAccess
                });
            }

            return Ok(result);
        }

        [HttpGet("reminder")]
        public async Task<IActionResult> Reminder(
            [FromHeader(Name = "X-User-Id")] Guid userId,
            [FromHeader(Name = "X-Company-Id")] Guid companyId,
            CancellationToken cancellationToken)
        {
            if (companyId == Guid.Empty)
                return BadRequest(new { message = "X-Company-Id is required." });

            var membership = await EnsureMembershipAsync(userId, companyId, cancellationToken);
            if (membership != null)
                return membership;

            var reminder = await _subscriptions.GetTrialReminderAsync(companyId, cancellationToken);
            return Ok(reminder ?? new TrialReminderInfo { CompanyId = companyId, IsDue = false });
        }

        private async Task<IActionResult?> EnsureMembershipAsync(
            Guid userId,
            Guid companyId,
            CancellationToken cancellationToken)
        {
            if (userId == Guid.Empty)
                return Unauthorized(new { message = SubscriptionMessages.CompanyContextRequired });

            if (!await _access.UserBelongsToCompanyAsync(userId, companyId, cancellationToken))
                return StatusCode(StatusCodes.Status403Forbidden, new { message = SubscriptionMessages.UserNotInCompany });

            return null;
        }
    }
}
