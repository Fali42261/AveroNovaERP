using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Domain.Constants;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Administration;

[QueryProperty(nameof(UserId), "id")]
public partial class UserViewPage : ContentPage
{
    private readonly IUserService _svc;
    private readonly IToastService _toasts;
    private readonly CurrentAccessService _access;
    private UserModel? _user;
    private bool _canUpdate;
    private bool _canDelete;
    private bool _deleting;

    public string? UserId { get; set; }

    public UserViewPage(IUserService svc, IToastService toasts, CurrentAccessService access)
    {
        InitializeComponent();
        _svc = svc;
        _toasts = toasts;
        _access = access;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _toasts.AttachTo(this);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        DetailsHost.Children.Clear();
        if (!Guid.TryParse(UserId, out var id))
            return;

        var snapshot = await _access.GetSnapshotAsync();
        _canUpdate = PermissionNames.Grants(snapshot.Permissions, PermissionNames.UsersUpdate);
        _canDelete = PermissionNames.Grants(snapshot.Permissions, PermissionNames.UsersDelete);

        _user = await _svc.GetByIdAsync(id);
        if (_user != null)
            BuildContent(_user);

        ApplyActionVisibility();
    }

    private void ApplyActionVisibility()
    {
        var protectedOwner = _user?.IsOwner == true;
        BtnEdit.IsVisible = _canUpdate && !protectedOwner;
        BtnDelete.IsVisible = _canDelete && !protectedOwner;
        BtnDelete.Text = _deleting ? "Deleting..." : "Delete";
        BtnDelete.IsEnabled = !_deleting;
    }

    private void BuildContent(UserModel u)
    {
        DetailsHost.Children.Clear();

        bool isActive = u.Status == UserStatus.Active;
        var profileCard = new Border { Style = (Style)Resources["AppCard"] };
        var pGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)),
            ColumnSpacing = 16
        };

        var av = new Border
        {
            WidthRequest = 60,
            HeightRequest = 60,
            BackgroundColor = Color.FromArgb("#EFF6FF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(30) }
        };
        av.Content = new Label
        {
            Text = u.AvatarInitials,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2563EB"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var info = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        info.Children.Add(new Label { Text = u.Name, FontSize = 18, FontAttributes = FontAttributes.Bold });
        info.Children.Add(new Label { Text = u.Email, FontSize = 13, TextColor = Color.FromArgb("#64748B") });
        info.Children.Add(new Label { Text = u.Phone, FontSize = 13, TextColor = Color.FromArgb("#64748B") });
        info.Children.Add(BuildBadge(u.IsOwner ? "Owner" : u.StatusLabel, isActive));
        pGrid.Add(av, 0, 0);
        pGrid.Add(info, 1, 0);
        profileCard.Content = pGrid;

        var detailCard = new Border { Style = (Style)Resources["AppCard"] };
        var dVsl = new VerticalStackLayout { Spacing = 12 };
        dVsl.Children.Add(new Label { Text = "Details", FontSize = 14, FontAttributes = FontAttributes.Bold });
        dVsl.Children.Add(new BoxView { Style = (Style)Resources["Divider"] });

        void AddDetail(string label, string value)
        {
            var g = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection(
                    new ColumnDefinition(new GridLength(130)),
                    new ColumnDefinition(GridLength.Star))
            };
            g.Add(new Label { Text = label, FontSize = 13, TextColor = Color.FromArgb("#64748B") }, 0, 0);
            g.Add(new Label { Text = value, FontSize = 13, FontAttributes = FontAttributes.Bold }, 1, 0);
            dVsl.Children.Add(g);
        }

        AddDetail("Full Name", u.Name);
        AddDetail("Email", u.Email);
        AddDetail("Mobile", string.IsNullOrWhiteSpace(u.Phone) ? "—" : u.Phone);
        AddDetail("Role", u.IsOwner ? "Owner" : u.Role);
        AddDetail("Status", u.StatusLabel);
        AddDetail("Created Date", u.CreatedDateLabel);
        AddDetail("Updated Date", u.UpdatedDateLabel);
        detailCard.Content = dVsl;

        DetailsHost.Children.Add(profileCard);
        DetailsHost.Children.Add(detailCard);
        ApplyActionVisibility();
    }

    private static Border BuildBadge(string text, bool active)
        => new()
        {
            BackgroundColor = active ? Color.FromArgb("#ECFDF5") : Color.FromArgb("#FEF2F2"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
            Padding = new Thickness(8, 3),
            HorizontalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 4, 0, 0),
            Content = new Label
            {
                Text = text,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = active ? Color.FromArgb("#059669") : Color.FromArgb("#DC2626")
            }
        };

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (_user == null || _user.IsOwner || !_canUpdate)
            return;
        await Shell.Current.GoToAsync($"{AppRoutes.UserEdit}?id={_user.LocalId}");
    }

    private async void OnBackClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (_user == null || _user.IsOwner || !_canDelete || _deleting)
            return;

        if (!await DialogHelper.ConfirmDeleteAsync("User", "Are you sure you want to delete this user?"))
            return;

        _deleting = true;
        ApplyActionVisibility();
        try
        {
            var (ok, error) = await _svc.DeleteAsync(_user.LocalId);
            if (!ok)
            {
                _toasts.ShowError("Unable to delete user.", error ?? "Please try again.");
                return;
            }

            _toasts.ShowSuccess("User deleted successfully.", string.Empty);
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            _deleting = false;
            ApplyActionVisibility();
        }
    }
}
