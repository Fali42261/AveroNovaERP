using AveroNova.App.UI.Controls.Notifications;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services;

public sealed class ToastService : IToastService
{
    private AppToastHost? _host;
    private ContentPage? _boundPage;
    private ToastRequest? _pending;

    public void ShowSuccess(string title, string message, TimeSpan? duration = null)
        => Show(title, message, ToastKind.Success, duration);

    public void ShowWarning(string title, string message)
        => Show(title, message, ToastKind.Warning);

    public void ShowError(string title, string message)
        => Show(title, message, ToastKind.Error);

    public void ShowInformation(string title, string message)
        => Show(title, message, ToastKind.Information);

    public void Show(string title, string message, ToastKind kind = ToastKind.Information, TimeSpan? duration = null)
    {
        var request = new ToastRequest(title, message, kind, duration);
        if (MainThread.IsMainThread)
            Present(request);
        else
            MainThread.BeginInvokeOnMainThread(() => Present(request));
    }

    public void AttachTo(ContentPage page)
    {
        void Bind()
        {
            _boundPage = page;
            FlushPending();
        }

        if (MainThread.IsMainThread)
            Bind();
        else
            MainThread.BeginInvokeOnMainThread(Bind);
    }

    public void Dismiss()
    {
        if (MainThread.IsMainThread)
            _host?.Dismiss();
        else
            MainThread.BeginInvokeOnMainThread(() => _host?.Dismiss());
    }

    private void Present(ToastRequest request)
    {
        var page = ResolvePage();
        if (page is null)
        {
            _pending = request;
            return;
        }

        _pending = null;
        _host = AppToastHost.Install(page);
        _host.Present(request.Title, request.Message, request.Kind, request.Duration);
    }

    private void FlushPending()
    {
        if (_pending is not { } pending)
            return;

        _pending = null;
        Present(pending);
    }

    private ContentPage? ResolvePage()
    {
        var current = GetCurrentContentPage();
        if (current is not null)
            return current;

        return _boundPage;
    }

    private static ContentPage? GetCurrentContentPage()
    {
        if (Shell.Current?.CurrentPage is ContentPage shellPage)
            return shellPage;

        var page = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page;
        return page as ContentPage
               ?? (page as Shell)?.CurrentPage as ContentPage;
    }

    private readonly record struct ToastRequest(string Title, string Message, ToastKind Kind, TimeSpan? Duration);
}
