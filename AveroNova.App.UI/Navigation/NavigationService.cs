namespace AveroNova.App.UI.Navigation;

public class NavigationService : INavigationService
{
    public Task NavigateToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        if (parameters is { Count: > 0 })
            return Shell.Current.GoToAsync(route, parameters);
        return Shell.Current.GoToAsync(route);
    }

    public Task GoBackAsync()
        => Shell.Current.GoToAsync("..");

    public Task NavigateToMainAsync()
        => Shell.Current.GoToAsync(AppRoutes.Main);

    public Task NavigateToLoginAsync()
        => Shell.Current.GoToAsync(AppRoutes.Welcome);
}
