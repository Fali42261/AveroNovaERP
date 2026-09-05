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
        var entry = new HostedPage(page, title, breadcrumb);
        _stack.Add(entry);
        await LoadAsync(entry);
        PageChanged?.Invoke(this, entry);
    }

    public async Task GoBackAsync()
    {
        if (_stack.Count <= 1) return;
        _stack.RemoveAt(_stack.Count - 1);
        var entry = _stack[^1];
        await LoadAsync(entry);
        PageChanged?.Invoke(this, entry);
    }

    private static Task LoadAsync(HostedPage entry)
        => entry.Page is IHostedPage hosted ? hosted.LoadForHostAsync() : Task.CompletedTask;
}
