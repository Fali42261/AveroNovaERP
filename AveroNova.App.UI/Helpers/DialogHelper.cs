namespace AveroNova.App.UI.Helpers;

/// <summary>
/// Centralized helper for common dialogs.
/// Every destructive action must confirm before proceeding.
/// </summary>
public static class DialogHelper
{
    /// <summary>
    /// Shows a delete confirmation dialog.
    /// Returns true only when the user explicitly confirms deletion.
    /// </summary>
    public static Task<bool> ConfirmDeleteAsync(string entityName, string? detail = null)
    {
        string message = string.IsNullOrEmpty(detail)
            ? $"Are you sure you want to delete this {entityName}? This action cannot be undone."
            : detail;

        return ConfirmAsync($"Delete {entityName}?", message, "Delete", "Cancel");
    }

    /// <summary>Generic confirmation dialog.</summary>
    public static Task<bool> ConfirmAsync(string title, string message,
        string accept = "Confirm", string cancel = "Cancel")
    {
        AppThemeSync.SyncFromCurrentApp();
        return Microsoft.Maui.Controls.Application.Current!.MainPage!.DisplayAlert(title, message, accept, cancel);
    }

    /// <summary>Shows an informational alert.</summary>
    public static Task AlertAsync(string title, string message, string ok = "OK")
    {
        AppThemeSync.SyncFromCurrentApp();
        return Microsoft.Maui.Controls.Application.Current!.MainPage!.DisplayAlert(title, message, ok);
    }

    /// <summary>Shows a success toast-style alert.</summary>
    public static Task SuccessAsync(string message)
        => AlertAsync("Success", message);

    /// <summary>Shows an error alert.</summary>
    public static Task ErrorAsync(string message)
        => AlertAsync("Error", message);
}
