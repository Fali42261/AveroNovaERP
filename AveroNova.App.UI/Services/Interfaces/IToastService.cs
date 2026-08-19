namespace AveroNova.App.UI.Services.Interfaces;

public enum ToastKind
{
    Success,
    Warning,
    Error,
    Information
}

public interface IToastService
{
    void Show(string title, string message, ToastKind kind = ToastKind.Information, TimeSpan? duration = null);
    void ShowSuccess(string title, string message, TimeSpan? duration = null);
    void ShowWarning(string title, string message);
    void ShowError(string title, string message);
    void ShowInformation(string title, string message);
    void AttachTo(ContentPage page);
    void Dismiss();
}
