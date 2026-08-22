using System.Collections.Specialized;
using AveroNova.App.UI.Layout;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Administration;

public partial class UsersListPage : ContentPage
{
    private readonly UsersViewModel _vm;
    private bool _isCompact;
    private bool _isExpanded;

    public UsersListPage(UsersViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
        if (Content != null)
            Content.BindingContext = vm;

        ErrorState.RetryClicked += async (_, _) => await _vm.LoadAsync();
        EmptyState.ActionClicked += (_, _) => _vm.AddCommand.Execute(null);

        _vm.AddRequested += (_, _) => _ = Shell.Current.GoToAsync(AppRoutes.UserAdd);
        _vm.ViewRequested += (_, user) => _ = Shell.Current.GoToAsync($"{AppRoutes.UserView}?id={user.LocalId}");
        _vm.EditRequested += (_, user) => _ = Shell.Current.GoToAsync($"{AppRoutes.UserEdit}?id={user.LocalId}");
        _vm.Items.CollectionChanged += OnItemsChanged;
        _vm.PropertyChanged += OnVmPropertyChanged;
        Root.SizeChanged += OnRootSizeChanged;
    }

    public async Task ReloadAsync()
    {
        await _vm.LoadAsync();
        OnRootSizeChanged(Root, EventArgs.Empty);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await _vm.LoadAsync(showLoading: false);
        if (sender is RefreshView refresh)
            refresh.IsRefreshing = false;
    }

    private void OnRootSizeChanged(object? sender, EventArgs e)
    {
        if (Root.Width <= 0)
            return;

        var size = ResponsiveBreakpoints.FromWidth(Root.Width);
        var compact = size == ScreenSize.Compact;
        var expanded = size == ScreenSize.Expanded;
        _vm.IsCompact = compact;

        if (compact)
        {
            Grid.SetColumn(RoleFilterHost, 0);
            Grid.SetRow(RoleFilterHost, 1);
            RoleFilterHost.WidthRequest = -1;
            Grid.SetColumn(StatusFilterHost, 0);
            Grid.SetRow(StatusFilterHost, 2);
            StatusFilterHost.WidthRequest = -1;
        }
        else
        {
            Grid.SetColumn(RoleFilterHost, 1);
            Grid.SetRow(RoleFilterHost, 0);
            RoleFilterHost.WidthRequest = 160;
            Grid.SetColumn(StatusFilterHost, 2);
            Grid.SetRow(StatusFilterHost, 0);
            StatusFilterHost.WidthRequest = 140;
        }

        if (compact != _isCompact || expanded != _isExpanded)
        {
            _isCompact = compact;
            _isExpanded = expanded;
            RenderList();
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => MainThread.BeginInvokeOnMainThread(RenderList);

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(UsersViewModel.CanUpdate)
            or nameof(UsersViewModel.CanDelete)
            or nameof(UsersViewModel.IsDeleting)
            or nameof(UsersViewModel.ShowList)
            or nameof(UsersViewModel.IsCompact))
        {
            MainThread.BeginInvokeOnMainThread(RenderList);
        }
    }

    private void RenderList()
    {
        ListHost.Children.Clear();
        if (!_vm.ShowList)
            return;

        if (_isCompact)
        {
            foreach (var user in _vm.Items)
                ListHost.Children.Add(BuildCard(user));
            return;
        }

        ListHost.Children.Add(BuildTableHeader());
        foreach (var user in _vm.Items)
            ListHost.Children.Add(BuildTableRow(user));
    }

    private View BuildTableHeader()
    {
        var grid = CreateRowGrid();
        var column = 0;
        AddHeader(grid, "Name", column++);
        AddHeader(grid, "Email", column++);
        AddHeader(grid, "Mobile", column++);
        AddHeader(grid, "Role", column++);
        AddHeader(grid, "Status", column++);
        AddHeader(grid, "Created Date", column++);
        AddHeader(grid, "Actions", column);
        return RowBorder(grid, header: true);
    }

    private View BuildTableRow(UserModel user)
    {
        var grid = CreateRowGrid();
        var column = 0;
        AddCell(grid, user.Name, column++, bold: true);
        AddCell(grid, Display(user.Email), column++);
        AddCell(grid, Display(user.Phone), column++);
        AddCell(grid, Display(user.Role), column++);
        grid.Add(BuildStatusBadge(user), column++);
        AddCell(grid, user.CreatedDateLabel, column++);
        grid.Add(BuildActions(user), column);
        return RowBorder(grid);
    }

    private View BuildCard(UserModel user)
    {
        var info = new VerticalStackLayout { Spacing = 4 };
        info.Children.Add(new Label
        {
            Text = user.Name,
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            TextColor = Res("TextPrimary", Colors.Black)
        });
        info.Children.Add(Labeled("Email", Display(user.Email)));
        info.Children.Add(Labeled("Role", Display(user.Role)));
        info.Children.Add(BuildStatusBadge(user));
        info.Children.Add(BuildActions(user));
        return RowBorder(info);
    }

    private Grid CreateRowGrid()
    {
        var defs = new ColumnDefinitionCollection
        {
            new(new GridLength(1.2, GridUnitType.Star)),
            new(new GridLength(1.4, GridUnitType.Star)),
            new(GridLength.Star),
            new(GridLength.Star),
            new(new GridLength(0.7, GridUnitType.Star)),
            new(new GridLength(0.9, GridUnitType.Star)),
            new(new GridLength(1.4, GridUnitType.Star))
        };
        return new Grid
        {
            ColumnDefinitions = defs,
            ColumnSpacing = 8,
            VerticalOptions = LayoutOptions.Center
        };
    }

    private View BuildActions(UserModel user)
    {
        var row = new HorizontalStackLayout
        {
            Spacing = 6,
            HorizontalOptions = LayoutOptions.Start
        };

        if (user.IsOwner)
        {
            row.Children.Add(new Label
            {
                Text = "Protected",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Res("TextSecondary", Colors.Gray),
                VerticalOptions = LayoutOptions.Center
            });
            var viewOwner = new Button { Text = "View", Style = TryStyle("SmallSecondaryButton") };
            viewOwner.Clicked += (_, _) => _vm.ViewCommand.Execute(user);
            row.Children.Add(viewOwner);
            return row;
        }

        var viewBtn = new Button { Text = "View", Style = TryStyle("SmallSecondaryButton") };
        viewBtn.Clicked += (_, _) => _vm.ViewCommand.Execute(user);
        row.Children.Add(viewBtn);

        if (_vm.CanEditUser(user))
        {
            var editBtn = new Button { Text = "Edit", Style = TryStyle("SmallButton") };
            editBtn.Clicked += (_, _) => _vm.EditCommand.Execute(user);
            row.Children.Add(editBtn);
        }

        if (_vm.CanDeleteUser(user))
        {
            var deleteBtn = new Button
            {
                Text = _vm.IsDeleting ? "Deleting..." : "Delete",
                Style = TryStyle("DangerButton"),
                HeightRequest = 36,
                FontSize = 12,
                IsEnabled = _vm.CanRunDelete
            };
            deleteBtn.Clicked += (_, _) => _ = _vm.DeleteCommand.ExecuteAsync(user);
            row.Children.Add(deleteBtn);
        }

        return row;
    }

    private View BuildStatusBadge(UserModel user)
    {
        var active = user.Status == UserStatus.Active;
        return new Border
        {
            Style = TryStyle("BadgeBase"),
            BackgroundColor = Res(active ? "SuccessBg" : "ErrorBg", Colors.Transparent),
            HorizontalOptions = LayoutOptions.Start,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
            Content = new Label
            {
                Text = user.IsOwner ? "Owner" : user.StatusLabel,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Res(active ? "SuccessText" : "ErrorText", Colors.Black)
            }
        };
    }

    private static View RowBorder(View content, bool header = false)
        => new Border
        {
            Style = TryStyle("ListRow"),
            Padding = header ? new Thickness(16, 10) : new Thickness(16, 12),
            Content = content
        };

    private static void AddHeader(Grid grid, string text, int column)
    {
        grid.Add(new Label
        {
            Text = text,
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = Res("TextSecondary", Colors.Gray)
        }, column, 0);
    }

    private static void AddCell(Grid grid, string text, int column, bool bold = false)
    {
        grid.Add(new Label
        {
            Text = text,
            FontSize = 12,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            LineBreakMode = LineBreakMode.TailTruncation,
            TextColor = Res("TextPrimary", Colors.Black)
        }, column, 0);
    }

    private static Label Labeled(string label, string value)
        => new()
        {
            Text = $"{label}: {value}",
            FontSize = 12,
            TextColor = Res("TextSecondary", Colors.Gray)
        };

    private static string Display(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static Color Res(string key, Color fallback)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is Color color)
        {
            return color;
        }

        return fallback;
    }

    private static Style? TryStyle(string key)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true
            && value is Style style)
        {
            return style;
        }

        return null;
    }
}
