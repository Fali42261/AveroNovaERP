using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;

namespace AveroNova.App.UI.Services;

/// <summary>
/// Central, cross-platform user feedback surface. Keep pages/view-models free from
/// platform-specific toast APIs and use this class for transient operation messages.
/// </summary>
public static class AppToast
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static Task SuccessAsync(string message, CancellationToken cancellationToken = default)
        => ShowAsync(message, ToastDuration.Short, cancellationToken);

    public static Task InfoAsync(string message, CancellationToken cancellationToken = default)
        => ShowAsync(message, ToastDuration.Short, cancellationToken);

    public static Task ErrorAsync(string message, CancellationToken cancellationToken = default)
        => ShowAsync(message, ToastDuration.Long, cancellationToken);

    private static async Task ShowAsync(
        string message,
        ToastDuration duration,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        await Gate.WaitAsync(cancellationToken);
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var toast = Toast.Make(message.Trim(), duration, 14);
                await toast.Show(cancellationToken);
            });
        }
        finally
        {
            Gate.Release();
        }
    }
}
