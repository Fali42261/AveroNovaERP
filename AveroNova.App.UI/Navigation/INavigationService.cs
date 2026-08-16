namespace AveroNova.App.UI.Navigation;

public interface INavigationService
{
    Task NavigateToAsync(string route, IDictionary<string, object>? parameters = null);
    Task GoBackAsync();
    Task NavigateToMainAsync();
    Task NavigateToLoginAsync();
}
