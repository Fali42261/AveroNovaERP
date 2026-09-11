using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AveroNova.App.UI.ViewModels;

public partial class LicenseViewModel : ObservableObject
{
    private readonly ILicenseService _licenses;

    [ObservableProperty] private string planName = "Free";
    [ObservableProperty] private string statusText = "Active";
    [ObservableProperty] private string remainingText = "Free plan active";
    [ObservableProperty] private string trialStartText = "—";
    [ObservableProperty] private string trialEndText = "—";
    [ObservableProperty] private bool isTrial;
    [ObservableProperty] private bool isExpired;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool hasError;

    public bool BusinessComingSoon => true;
    public bool EnterpriseComingSoon => true;

    public LicenseViewModel(ILicenseService licenses)
    {
        _licenses = licenses;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ErrorMessage = null;
        HasError = false;
        try
        {
            await _licenses.ValidateOnlineIfPossibleAsync();
            var state = await _licenses.GetAccessStateAsync();

            PlanName = string.IsNullOrWhiteSpace(state.Plan)
                ? "Free"
                : state.Plan.Equals("Starter", StringComparison.OrdinalIgnoreCase)
                    ? "Free"
                    : state.Plan;

            IsTrial = state.IsTrial && state.Status == LicenseStatus.Trial;
            IsExpired = state.Status == LicenseStatus.Expired;

            if (PlanName.Equals("Free", StringComparison.OrdinalIgnoreCase))
            {
                StatusText = state.Status == LicenseStatus.Expired ? "Inactive" : "Active";
                RemainingText = state.Status == LicenseStatus.Expired
                    ? "Free plan access needs to be refreshed"
                    : "Free plan active";
                TrialStartText = "Not applicable";
                TrialEndText = "Not applicable";
            }
            else
            {
                StatusText = state.Status.ToString();
                RemainingText = IsExpired
                    ? "Plan expired"
                    : IsTrial
                        ? $"{state.RemainingTrialDays} day{(state.RemainingTrialDays == 1 ? string.Empty : "s")} remaining"
                        : "Plan active";
                TrialStartText = FormatLocal(state.TrialStartDateUtc);
                TrialEndText = FormatLocal(state.TrialEndDateUtc);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[License] View model load failed: {ex}");
            ErrorMessage = "Unable to load plan status.";
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FormatLocal(DateTime? utc)
    {
        if (utc is not DateTime value) return "—";
        var local = value.Kind == DateTimeKind.Utc
            ? value.ToLocalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime();
        return local.ToString("dd-MMM-yyyy");
    }
}
