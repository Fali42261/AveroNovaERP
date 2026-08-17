using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AveroNova.App.UI.ViewModels;

public partial class LicenseViewModel : ObservableObject
{
    private readonly ILicenseService _licenses;

    [ObservableProperty] private string planName = "Starter";
    [ObservableProperty] private string statusText = "Trial";
    [ObservableProperty] private string remainingText = string.Empty;
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
        if (IsBusy)
            return;

        IsBusy = true;
        ErrorMessage = null;
        HasError = false;
        try
        {
            await _licenses.ValidateOnlineIfPossibleAsync();
            var state = await _licenses.GetAccessStateAsync();
            PlanName = string.IsNullOrWhiteSpace(state.Plan) ? "Starter" : state.Plan;
            StatusText = state.Status.ToString();
            IsTrial = state.IsTrial && state.Status == LicenseStatus.Trial;
            IsExpired = state.Status == LicenseStatus.Expired;
            RemainingText = IsExpired
                ? "Trial expired"
                : IsTrial
                    ? $"{state.RemainingTrialDays} day{(state.RemainingTrialDays == 1 ? "" : "s")} remaining"
                    : "Licensed";
            TrialStartText = FormatLocal(state.TrialStartDateUtc);
            TrialEndText = FormatLocal(state.TrialEndDateUtc);
        }
        catch (Exception)
        {
            ErrorMessage = "Unable to load license status.";
            HasError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string FormatLocal(DateTime? utc)
    {
        if (utc is not DateTime value)
            return "—";
        var local = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime();
        return local.ToString("dd-MMM-yyyy");
    }
}
