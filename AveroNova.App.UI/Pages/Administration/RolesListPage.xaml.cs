using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Administration;

public partial class RolesListPage : ContentPage, IHostedPage
{
    private readonly IUserService _svc;
    private readonly ICompanyService _company;
    private readonly IMainContentNavigator _navigator;
    private readonly Func<RoleFormPage> _formFactory;
    private List<RoleModel> _all = [];
    private List<UserModel> _users = [];
    private bool _loading;

    public RolesListPage(
        IUserService svc,
        ICompanyService company,
        IMainContentNavigator navigator,
        Func<RoleFormPage> formFactory)
    {
        InitializeComponent();
        _svc = svc;
        _company = company;
        _navigator = navigator;
        _formFactory = formFactory;
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
            if (companyId == Guid.Empty)
            {
                _all = [];
                _users = [];
            }
            else
            {
                _all = await _svc.GetRolesAsync(companyId);
                _users = await _svc.GetAllAsync(companyId);
            }
            RenderList(_all);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Roles] Load failed: {ex}");
            _all = [];
            _users = [];
            RoleList.Children.Clear();
            RoleList.Children.Add(new Label
            {
                Text = "Roles could not be loaded. Pull down to retry.",
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

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        try { await LoadForHostAsync(); }
        finally { Refresher.IsRefreshing = false; }
    }

    private async void OnRefreshClicked(object? sender, EventArgs e) => await LoadForHostAsync();

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        var q = e.NewTextValue?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(q))
        {
            RenderList(_all);
            return;
        }

        var shown = _all.Where(r =>
                r.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (r.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                AssignedUsers(r).Any(u => u.Name.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        RenderList(shown);
    }

    private void RenderList(List<RoleModel> items)
    {
        LblCount.Text = $"{items.Count} role{(items.Count == 1 ? string.Empty : "s")}";
        RoleList.Children.Clear();

        if (items.Count == 0)
        {
            RoleList.Children.Add(new Label
            {
                Text = "No roles found.",
                FontSize = 14,
                TextColor = Color.FromArgb("#64748B"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 40)
            });
            return;
        }

        foreach (var role in items.OrderBy(r => r.Name))
            RoleList.Children.Add(BuildRow(role));
    }

    private List<UserModel> AssignedUsers(RoleModel role) =>
        _users.Where(u => u.RoleId == role.LocalId)
            .OrderBy(u => u.Name)
            .ToList();

    private View BuildRow(RoleModel role)
    {
        var assignedUsers = AssignedUsers(role);
        var assignedText = assignedUsers.Count == 0
            ? "Assigned to: No users"
            : $"Assigned to: {string.Join(", ", assignedUsers.Select(u => u.Name))}";

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

        var iconBorder = new Border
        {
            WidthRequest = 42,
            HeightRequest = 42,
            BackgroundColor = Color.FromArgb("#F3E8FF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(21) },
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = "🛡",
                FontSize = 18,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };

        var info = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
        info.Children.Add(new Label { Text = role.Name, FontSize = 14, FontAttributes = FontAttributes.Bold });
        info.Children.Add(new Label
        {
            Text = role.Description ?? "No description",
            FontSize = 12,
            TextColor = Color.FromArgb("#64748B")
        });
        info.Children.Add(new Label
        {
            Text = assignedText,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = assignedUsers.Count == 0 ? Color.FromArgb("#94A3B8") : Color.FromArgb("#2563EB"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        info.Children.Add(new Label
        {
            Text = $"{assignedUsers.Count} user{(assignedUsers.Count == 1 ? string.Empty : "s")}",
            FontSize = 11,
            TextColor = Color.FromArgb("#94A3B8")
        });

        var actions = new HorizontalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };

        var editBtn = MakeActionButton("Edit", true);
        editBtn.Clicked += async (_, _) =>
        {
            try
            {
                var page = _formFactory();
                page.EditId = role.LocalId.ToString("D");
                await _navigator.NavigateAsync(page, "Edit Role", "Home / Roles / Edit");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Roles] Edit failed: {ex}");
                await DisplayAlert("Roles", "Role could not be opened.", "OK");
            }
        };

        var deleteBtn = MakeActionButton("Delete", false);
        deleteBtn.IsEnabled = !role.IsSystem;
        deleteBtn.Clicked += async (_, _) =>
        {
            try
            {
                var result = await _svc.DeleteRoleAsync(role.LocalId);
                if (result.Ok) await LoadForHostAsync();
                else await DisplayAlert("Delete failed", result.Error ?? "Unable to delete role.", "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Roles] Delete failed: {ex}");
                await DisplayAlert("Roles", "Role could not be deleted.", "OK");
            }
        };

        actions.Children.Add(editBtn);
        actions.Children.Add(deleteBtn);

        grid.Add(iconBorder, 0, 0);
        grid.Add(info, 1, 0);
        grid.Add(actions, 2, 0);
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
        try { await _navigator.NavigateAsync(_formFactory(), "Add Role", "Home / Roles / Add"); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Roles] Add failed: {ex}");
            await DisplayAlert("Roles", "Role form could not be opened.", "OK");
        }
    }
}
