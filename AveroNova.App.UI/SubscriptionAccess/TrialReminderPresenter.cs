using AveroNova.Application.Interfaces;
using AveroNova.App.UI.Services.Local;
using AveroNova.Domain.Constants;

namespace AveroNova.App.UI.SubscriptionAccess;

public sealed class TrialReminderPresenter
{
    private readonly ICompanySubscriptionService _subscriptions;

    public TrialReminderPresenter(ICompanySubscriptionService subscriptions)
    {
        _subscriptions = subscriptions;
    }

    public async Task ShowIfDueAsync(Page? page, CancellationToken cancellationToken = default)
    {
        if (page == null)
            return;

        var userId = LocalSessionStore.UserId ?? Guid.Empty;
        var companyId = LocalSessionStore.CompanyId ?? Guid.Empty;
        if (userId == Guid.Empty || companyId == Guid.Empty)
            return;

        var reminder = await _subscriptions.GetTrialReminderAsync(companyId, cancellationToken);
        if (reminder == null || !reminder.IsDue)
            return;

        if (!TrialReminderState.TryBeginShow(userId, companyId, reminder.EndDate))
            return;

        var expiryText = reminder.EndDate.ToString("dd MMMM yyyy");
        var message =
            $"Your AveroNova Free Trial will expire on:\n\n{expiryText}\n\nAccess will be restricted after the trial expires.";

        var continueRequested = await page.DisplayAlertAsync(
            SubscriptionMessages.FreeTrialExpiresTomorrow,
            message,
            "Continue Subscription",
            "OK");

        if (continueRequested)
        {
            await page.DisplayAlertAsync(
                "Continue Subscription",
                SubscriptionMessages.PaidSubscriptionComingSoon,
                "OK");
        }
    }
}
