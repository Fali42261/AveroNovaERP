using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Administration;

public partial class UsersRolesPage : ContentPage
{
    private readonly Func<UsersListPage> _usersFactory;
    private readonly Func<RolesListPage> _rolesFactory;
    private UsersListPage? _usersPage;
    private RolesListPage? _rolesPage;
    private bool _rolesSelected;

    private readonly IToastService _toasts;

    public UsersRolesPage(
        Func<UsersListPage> usersFactory,
        Func<RolesListPage> rolesFactory,
        IToastService toasts)
    {
        InitializeComponent();
        _usersFactory = usersFactory;
        _rolesFactory = rolesFactory;
        _toasts = toasts;
    }

    public Task ReloadAsync() => ShowTabAsync(roles: _rolesSelected);

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _toasts.AttachTo(this);
        await ReloadAsync();
    }

    private async void OnUsersTabClicked(object? sender, EventArgs e)
        => await ShowTabAsync(roles: false);

    private async void OnRolesTabClicked(object? sender, EventArgs e)
        => await ShowTabAsync(roles: true);

    private async Task ShowTabAsync(bool roles)
    {
        _rolesSelected = roles;
        BtnUsers.Style = AppStyle(roles ? "SmallSecondaryButton" : "PrimaryButton");
        BtnRoles.Style = AppStyle(roles ? "PrimaryButton" : "SmallSecondaryButton");

        if (roles)
        {
            _rolesPage ??= _rolesFactory();
            TabHost.Content = _rolesPage.Content;
            await _rolesPage.ReloadAsync();
            return;
        }

        _usersPage ??= _usersFactory();
        TabHost.Content = _usersPage.Content;
        await _usersPage.ReloadAsync();
    }

    private static Style AppStyle(string key)
        => (Style)Microsoft.Maui.Controls.Application.Current!.Resources[key];
}
