using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Administration;

public partial class UsersListPage : ContentPage, IHostedPage
{
    private readonly IUserService _svc;
    private readonly ICompanyService _company;
    private readonly ISubscriptionService _subscriptions;
    private readonly IMainContentNavigator _navigator;
    private readonly Func<UserFormPage> _formFactory;
    private readonly Func<UserViewPage> _viewFactory;
    private List<UserModel> _all = [];
    private bool _loading;
    private int _maxUsers = 2;

    public UsersListPage(
        IUserService svc,
        ICompanyService company,
        ISubscriptionService subscriptions,
        IMainContentNavigator navigator,
        Func<UserFormPage> formFactory,
        Func<UserViewPage> viewFactory)
    {
        InitializeComponent();
        _svc = svc;
        _company = company;
        _subscriptions = subscriptions;
        _navigator = navigator;
        _formFactory = formFactory;
        _viewFactory = viewFactory;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadForHostAsync();
    }

    public async Task LoadForHostAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            var companyId = _company.CurrentCompany?.LocalId ?? Guid.Empty;
            _all = companyId == Guid.Empty ? [] : await _svc.GetAllAsync(companyId);

            var subscription = companyId == Guid.Empty ? null : await _subscriptions.GetCurrentAsync(companyId);
            _maxUsers = subscription?.MaxUsers ?? 2;
            ApplyPlanLimit();
            RenderList(_all);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Users] Load failed: {ex}");
            _all = [];
            BtnAdd.IsEnabled = false;
            UserList.Children.Clear();
            UserList.Children.Add(new Label
            {
                Text = "Users could not be loaded. Pull down to retry.",
                FontSize = 14,
                TextColor = Color.FromArgb("#DC2626"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 40)
            });
        }
        finally
        {
            _loading = false;
        }
    }

    private void ApplyPlanLimit()
    {
        var unlimited = _maxUsers < 0;
        var reached = !unlimited && _all.Count >= _maxUsers;
        BtnAdd.IsEnabled = !reached;
        BtnAdd.Opacity = reached ? 0.5 : 1;
        PlanLimitBanner.IsVisible = reached;

        if (unlimited)
        {
            LblPlanLimit.Text = string.Empty;
            return;
        }

        LblPlanLimit.Text = reached
            ? $"Free plan limit reached: {_maxUsers} users total (owner/admin included). Upgrade to add more users."
            : $"Free plan: {_all.Count} of {_maxUsers} users used.";
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        try { await LoadForHostAsync(); }
        finally { Refresher.IsRefreshing = false; }
    }

    private async void OnRefreshClicked(object? sender, EventArgs e) => await LoadForHostAsync();

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            RenderList(_all);
            return;
        }

        var shown = _all.Where(u =>
                u.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                u.Phone.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                u.Role.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        RenderList(shown);
    }

    private void RenderList(List<UserModel> items)
    {
        LblCount.Text = $"{items.Count} user{(items.Count == 1 ? string.Empty : "s")}";
        UserList.Children.Clear();

        if (items.Count == 0)
        {
            UserList.Children.Add(new Label
            {
                Text = "No users found.",
                FontSize = 14,
                TextColor = Color.FromArgb("#64748B"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 40)
            });
            return;
        }

        foreach (var user in items.OrderBy(u => u.Name))
            UserList.Children.Add(BuildRow(user));
    }

    private View BuildRow(UserModel user)
    {
        var isActive = user.Status == UserStatus.Active;
        var border = new Border
        {
            BackgroundColor = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#1E293B") : Colors.White,
            Stroke = Color.FromArgb("#E2E8F0"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Padding = new Thickness(14, 12)
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 12
        };

        var avatar = new Border
        {
            WidthRequest = 42,
            HeightRequest = 42,
            BackgroundColor = Color.FromArgb("#EFF6FF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(21) },
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = user.AvatarInitials,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#2563EB"),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };

        var info = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        info.Children.Add(new Label { Text = user.Name, FontSize = 14, FontAttributes = FontAttributes.Bold });
        info.Children.Add(new Label { Text = user.Email, FontSize = 12, TextColor = Color.FromArgb("#64748B") });
        info.Children.Add(new Label { Text = user.Role, FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });

        var right = new VerticalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };
        right.Children.Add(new Label
        {
            Text = user.StatusLabel,
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = isActive ? Color.FromArgb("#059669") : Color.FromArgb("#DC2626"),
            HorizontalOptions = LayoutOptions.End
        });

        var actions = new HorizontalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.End };
        var viewButton = MakeActionButton("View", false);
        viewButton.Clicked += async (_, _) =>
        {
            try
            {
                var page = _viewFactory();
                page.UserId = user.LocalId.ToString("D");
                await _navigator.NavigateAsync(page, "User Details", "Home / Users / Details");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Users] View failed: {ex}");
                await DisplayAlert("Users", "User details could not be opened.", "OK");
            }
        };

        var editButton = MakeActionButton("Edit", true);
        editButton.Clicked += async (_, _) =>
        {
            try
            {
                var page = _formFactory();
                page.EditId = user.LocalId.ToString("D");
                await _navigator.NavigateAsync(page, "Edit User", "Home / Users / Edit");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Users] Edit failed: {ex}");
                await DisplayAlert("Users", "User could not be opened for editing.", "OK");
            }
        };
        actions.Children.Add(viewButton);
        actions.Children.Add(editButton);
        right.Children.Add(actions);

        grid.Add(avatar, 0, 0);
        grid.Add(info, 1, 0);
        grid.Add(right, 2, 0);
        border.Content = grid;
        return border;
    }

    private static Button MakeActionButton(string text, bool primary) => new()
    {
        Text = text,
        FontSize = 12,
        HeightRequest = 36,
        Padding = new Thickness(12, 0),
        CornerRadius = 8,
        BackgroundColor = primary ? Color.FromArgb("#2563EB") : Colors.Transparent,
        TextColor = primary ? Colors.White : Color.FromArgb("#2563EB"),
        BorderColor = Color.FromArgb("#2563EB"),
        BorderWidth = 1
    };

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        if (_maxUsers >= 0 && _all.Count >= _maxUsers)
        {
            await DisplayAlert("Free plan limit", $"You can have up to {_maxUsers} users total on the Free plan, including the owner/admin.", "OK");
            return;
        }

        try { await _navigator.NavigateAsync(_formFactory(), "Add User", "Home / Users / Add"); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Users] Add failed: {ex}");
            await DisplayAlert("Users", "User form could not be opened.", "OK");
        }
    }

    private async void OnFilterClicked(object? sender, EventArgs e)
        => await DisplayAlert("Filter", "Use search to filter users in this release.", "OK");
}
