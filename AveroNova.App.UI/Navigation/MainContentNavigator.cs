namespace AveroNova.App.UI.Navigation;

public sealed record HostedPage(ContentPage Page, string Title, string Breadcrumb);

public interface IHostedPage
{
    Task LoadForHostAsync();
}

public interface IMainContentNavigator
{
    event EventHandler<HostedPage>? PageChanged;
    HostedPage? Current { get; }
    void SetRoot(ContentPage page, string title, string breadcrumb);
    Task NavigateAsync(ContentPage page, string title, string breadcrumb);
    Task GoBackAsync();
}

/// <summary>
/// Navigation stack for ERP pages displayed inside MainLayoutView. It avoids
/// Shell push animations and keeps the sidebar/header visible.
/// </summary>
public sealed class MainContentNavigator : IMainContentNavigator
{
    private readonly List<HostedPage> _stack = [];

    public event EventHandler<HostedPage>? PageChanged;
    public HostedPage? Current => _stack.Count == 0 ? null : _stack[^1];

    public void SetRoot(ContentPage page, string title, string breadcrumb)
    {
        _stack.Clear();
        _stack.Add(new HostedPage(page, title, breadcrumb));
    }

    public async Task NavigateAsync(ContentPage page, string title, string breadcrumb)
    {
        TryReleaseInputFocus(Current?.Page);
        var entry = new HostedPage(page, title, breadcrumb);
        _stack.Add(entry);

        try
        {
            await LoadAsync(entry);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HostedNavigation] Initial load failed for {title}: {ex}");
        }

        SafeRaisePageChanged(entry);
    }

    public async Task GoBackAsync()
    {
        TryReleaseInputFocus(Current?.Page);
        if (_stack.Count <= 1) return;

        _stack.RemoveAt(_stack.Count - 1);
        var entry = _stack[^1];

        // Show the previous page first so a refresh failure can never make a
        // successful Save look like the app closed or left the hosted layout.
        SafeRaisePageChanged(entry);

        try
        {
            await LoadAsync(entry);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HostedNavigation] Reload failed for {entry.Title}: {ex}");
        }
    }

    private void SafeRaisePageChanged(HostedPage entry)
    {
        try
        {
            PageChanged?.Invoke(this, entry);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HostedNavigation] PageChanged failed for {entry.Title}: {ex}");
        }
    }

    private static void TryReleaseInputFocus(Page? page)
    {
        try
        {
            page?.Unfocus();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HostedNavigation] Unfocus failed: {ex}");
        }
    }

    private static Task LoadAsync(HostedPage entry)
        => entry.Page is IHostedPage hosted ? hosted.LoadForHostAsync() : Task.CompletedTask;
}
