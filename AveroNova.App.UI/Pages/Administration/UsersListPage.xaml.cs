using AveroNova.App.UI.Controls.Common;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Resources;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Administration;

public partial class UsersListPage : ContentPage
{
    private readonly IUserService _svc;
    private readonly ICompanyService _company;

    private List<UserModel> _all = [];

    public UsersListPage(
        IUserService svc,
        ICompanyService company)
    {
        InitializeComponent();

        _svc = svc;
        _company = company;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await LoadAsync();
    }

    private async void OnRefreshing(
        object sender,
        EventArgs e)
    {
        await LoadAsync();

        Refresher.IsRefreshing = false;
    }

    private async void OnRefreshClicked(
        object sender,
        EventArgs e)
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var companyId = _company.CurrentCompany?.LocalId;

        if (!companyId.HasValue)
        {
            _all = [];
            RenderList(_all);
            return;
        }

        _all = await _svc.GetAllAsync(companyId.Value);

        RenderList(_all);
    }

    private void OnSearchChanged(
        object sender,
        TextChangedEventArgs e)
    {
        var query = e.NewTextValue?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(query))
        {
            RenderList(_all);
            return;
        }

        // Local filtering.
        // No SearchAsync() is required because IUserService
        // currently exposes GetAllAsync(companyId) only.
        var shown = _all
            .Where(u =>
                u.Name.Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase)
                ||
                u.Email.Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase)
                ||
                u.Phone.Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase)
                ||
                u.Role.Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase)
                ||
                u.CompanyName.Contains(
                    query,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();

        RenderList(shown);
    }

    private void RenderList(List<UserModel> items)
    {
        LblCount.Text =
            $"{items.Count} user{(items.Count == 1 ? "" : "s")}";

        UserList.Children.Clear();

        if (items.Count == 0)
        {
            UserList.Children.Add(
                new Label
                {
                    Text = "No users found.",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#64748B"),
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 40)
                });

            return;
        }

        foreach (var user in items)
        {
            UserList.Children.Add(BuildRow(user));
        }
    }

    private View BuildRow(UserModel user)
    {
        bool isActive =
            user.Status == UserStatus.Active;

        var border = new Border
        {
            BackgroundColor =
                Microsoft.Maui.Controls.Application.Current?
                    .RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#1E293B")
                    :Color.FromArgb("#FFFFFF"),

            Stroke = Color.FromArgb("#E2E8F0"),
            StrokeThickness = 1,

            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(12)
            },

            Padding = new Thickness(14, 12)
        };

        var grid = new Grid
        {
            ColumnDefinitions =
                new ColumnDefinitionCollection(
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)),

            ColumnSpacing = 12
        };

        // ---------------------------------------------------------
        // Avatar
        // ---------------------------------------------------------

        var avatar = new Border
        {
            WidthRequest = 42,
            HeightRequest = 42,
            BackgroundColor = Color.FromArgb("#EFF6FF"),
            StrokeThickness = 0,

            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(21)
            },

            VerticalOptions = LayoutOptions.Center
        };

        avatar.Content = new Label
        {
            Text = user.AvatarInitials,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2563EB"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        // ---------------------------------------------------------
        // User information
        // ---------------------------------------------------------

        var info = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center
        };

        info.Children.Add(
            new Label
            {
                Text = user.Name,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold
            });

        info.Children.Add(
            new Label
            {
                Text = user.Email,
                FontSize = 12,
                TextColor = Color.FromArgb("#64748B")
            });

        info.Children.Add(
            new Label
            {
                Text = user.Role,
                FontSize = 11,
                TextColor = Color.FromArgb("#94A3B8")
            });

        // ---------------------------------------------------------
        // Right side
        // ---------------------------------------------------------

        var right = new VerticalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };

        var statusBadge = new Border
        {
            BackgroundColor = isActive
                ? Color.FromArgb("#ECFDF5")
                : Color.FromArgb("#FEF2F2"),

            StrokeThickness = 0,

            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(999)
            },

            Padding = new Thickness(7, 2)
        };

        statusBadge.Content = new Label
        {
            Text = user.StatusLabel,
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,

            TextColor = isActive
                ? Color.FromArgb("#059669")
                : Color.FromArgb("#DC2626")
        };

        right.Children.Add(statusBadge);

        // ---------------------------------------------------------
        // Actions
        // ---------------------------------------------------------

        var actionsRow = new HorizontalStackLayout
        {
            Spacing = 6,
            HorizontalOptions = LayoutOptions.End
        };

        var viewButton = new Button
        {
            Text = "View",
            Style = (Style)Resources["SmallSecondaryButton"]
        };

        viewButton.Clicked += async (_, _) =>
        {
            await Shell.Current.GoToAsync(
                $"{AppRoutes.UserView}?id={user.LocalId}");
        };

        var editButton = new Button
        {
            Text = "Edit",
            Style = (Style)Resources["SmallButton"]
        };

        editButton.Clicked += async (_, _) =>
        {
            await Shell.Current.GoToAsync(
                $"{AppRoutes.UserEdit}?id={user.LocalId}");
        };

        actionsRow.Children.Add(viewButton);
        actionsRow.Children.Add(editButton);

        right.Children.Add(actionsRow);

        // ---------------------------------------------------------
        // Grid
        // ---------------------------------------------------------

        grid.Add(avatar, 0, 0);
        grid.Add(info, 1, 0);
        grid.Add(right, 2, 0);

        border.Content = grid;

        return border;
    }

    private async void OnAddClicked(
        object sender,
        EventArgs e)
    {
        await Shell.Current.GoToAsync(
            AppRoutes.UserAdd);
    }

    private async void OnFilterClicked(
        object sender,
        EventArgs e)
    {
        await DisplayAlert(
            "Filter",
            "Filter options coming soon.",
            "OK");
    }
}