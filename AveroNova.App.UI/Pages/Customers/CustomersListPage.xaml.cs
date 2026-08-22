using System.Collections.Specialized;
using AveroNova.App.UI.Layout;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Customers;

public partial class CustomersListPage : ContentPage
{
    private const double StatusControlWidth = 180;
    private const double ActionReserveWidth = 168;
    private const double ToolbarGutter = 32;
    private const double ToolbarColumnSpacing = 12;

    private readonly CustomersViewModel _list;
    private readonly CustomerFormViewModel _form;
    private readonly CustomerViewViewModel _details;
    private bool _isCompact;
    private bool _isExpanded;

    public CustomersListPage(
        CustomersViewModel list,
        CustomerFormViewModel form,
        CustomerViewViewModel details)
    {
        InitializeComponent();
        _list = list;
        _form = form;
        _details = details;

        BindingContext = list;
        if (Content != null)
            Content.BindingContext = list;

        FormView.BindingContext = form;
        DetailsView.BindingContext = details;

        ErrorState.RetryClicked += async (_, _) => await _list.RetryCommand.ExecuteAsync(null);
        EmptyState.ActionClicked += (_, _) => _list.AddCommand.Execute(null);

        _list.AddRequested += (_, _) => _ = ShowFormAsync(null);
        _list.ViewRequested += (_, customer) => _ = ShowDetailsAsync(customer.LocalId);
        _list.EditRequested += (_, customer) => _ = ShowFormAsync(customer.LocalId);
        _list.Items.CollectionChanged += OnItemsChanged;
        _list.PropertyChanged += OnListPropertyChanged;

        _form.Saved += (_, _) =>
        {
            if (!_form.IsEditMode)
                _list.ResetPaging();
            ShowList(reload: true);
        };
        _form.Cancelled += (_, _) => ShowList(reload: false);
        _details.BackRequested += (_, _) => ShowList(reload: false);
        _details.EditRequested += (_, id) => _ = ShowFormAsync(id);
        _details.Deleted += (_, _) => ShowList(reload: true);

        Root.SizeChanged += OnRootSizeChanged;
        ShowList(reload: false);
    }

    public Task ReloadAsync()
    {
        ShowList(reload: true);
        return Task.CompletedTask;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (ListPanel.IsVisible)
            await _list.LoadAsync();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await _list.LoadAsync(showLoading: false);
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
        _list.IsCompact = compact;
        _details.IsInlineLayout = !compact;
        ApplyToolbarLayout(compact);
        if (StatusDropdownLayer.IsVisible)
            PositionStatusDropdown();

        if (compact != _isCompact || expanded != _isExpanded)
        {
            _isCompact = compact;
            _isExpanded = expanded;
            RenderList();
        }
    }

    private void ApplyToolbarLayout(bool compact)
    {
        if (compact)
        {
            Grid.SetColumn(SearchHost, 0);
            Grid.SetRow(SearchHost, 0);
            Grid.SetColumnSpan(SearchHost, 3);
            SearchHost.WidthRequest = -1;
            SearchHost.MaximumWidthRequest = double.PositiveInfinity;
            SearchHost.HorizontalOptions = LayoutOptions.Fill;

            Grid.SetColumn(StatusHost, 0);
            Grid.SetRow(StatusHost, 1);
            Grid.SetColumnSpan(StatusHost, 3);
            StatusHost.WidthRequest = -1;
            StatusHost.MinimumWidthRequest = 0;
            StatusHost.MaximumWidthRequest = double.PositiveInfinity;
            StatusHost.HorizontalOptions = LayoutOptions.Fill;

            Grid.SetColumn(ActionHost, 0);
            Grid.SetRow(ActionHost, 2);
            Grid.SetColumnSpan(ActionHost, 3);
            ActionHost.HorizontalOptions = LayoutOptions.Start;
            ActionLabel.IsVisible = false;
            return;
        }

        Grid.SetColumn(SearchHost, 0);
        Grid.SetRow(SearchHost, 0);
        Grid.SetColumnSpan(SearchHost, 1);
        SearchHost.HorizontalOptions = LayoutOptions.Start;
        ApplySearchWidth();

        Grid.SetColumn(StatusHost, 1);
        Grid.SetRow(StatusHost, 0);
        Grid.SetColumnSpan(StatusHost, 1);
        StatusHost.WidthRequest = StatusControlWidth;
        StatusHost.MinimumWidthRequest = 160;
        StatusHost.MaximumWidthRequest = 200;
        StatusHost.HorizontalOptions = LayoutOptions.Start;

        Grid.SetColumn(ActionHost, 2);
        Grid.SetRow(ActionHost, 0);
        Grid.SetColumnSpan(ActionHost, 1);
        ActionHost.HorizontalOptions = LayoutOptions.Start;
        ActionLabel.IsVisible = true;
    }

    private void ApplySearchWidth()
    {
        var innerWidth = ToolbarGrid.Width > 1
            ? ToolbarGrid.Width
            : Math.Max(Root.Width - ToolbarGutter, 0);
        if (innerWidth <= 0)
            return;

        var actionReserve = _list.ShowAddButton
            ? ActionReserveWidth + ToolbarColumnSpacing
            : 0;
        var reserved = StatusControlWidth + ToolbarColumnSpacing + actionReserve;
        var maxFit = Math.Max(0, innerWidth - reserved);
        var upper = Math.Max(200, Math.Min(innerWidth * 0.65, maxFit));
        var lower = Math.Min(240, upper);
        var searchWidth = Math.Clamp(innerWidth * 0.60, lower, upper);

        SearchHost.WidthRequest = searchWidth;
        SearchHost.MaximumWidthRequest = innerWidth * 0.65;
    }

    private void OnStatusFieldTapped(object? sender, TappedEventArgs e)
    {
        if (_list.IsDeleting)
            return;

        if (StatusDropdownLayer.IsVisible)
        {
            CloseStatusDropdown();
            return;
        }

        OpenStatusDropdown();
    }

    private void OnStatusDropdownDismissed(object? sender, TappedEventArgs e)
        => CloseStatusDropdown();

    private void OpenStatusDropdown()
    {
        StatusDropdownItems.Children.Clear();
        foreach (var option in _list.StatusFilters)
        {
            var selected = option.Equals(_list.SelectedStatusFilter, StringComparison.OrdinalIgnoreCase);
            var label = new Label
            {
                Text = option,
                FontSize = 13,
                LineBreakMode = LineBreakMode.TailTruncation,
                VerticalOptions = LayoutOptions.Center,
                InputTransparent = true,
                TextColor = Res("TextPrimary", Colors.Black)
            };
            var row = new Border
            {
                Padding = new Thickness(12, 0),
                HeightRequest = 40,
                MinimumHeightRequest = 40,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Content = label
            };
            row.SetAppThemeColor(
                Border.BackgroundColorProperty,
                selected ? Res("Gray100", Color.FromArgb("#F3F4F6")) : Colors.Transparent,
                selected ? Res("InputBackgroundDark", Color.FromArgb("#1E293B")) : Colors.Transparent);
            var captured = option;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => SelectStatusFilter(captured);
            row.GestureRecognizers.Add(tap);
            StatusDropdownItems.Children.Add(row);
        }

        StatusDropdownLayer.IsVisible = true;
        Dispatcher.Dispatch(PositionStatusDropdown);
    }

    private void SelectStatusFilter(string option)
    {
        _list.SelectedStatusFilter = option;
        CloseStatusDropdown();
    }

    private void CloseStatusDropdown()
    {
        if (!StatusDropdownLayer.IsVisible)
            return;

        StatusDropdownLayer.IsVisible = false;
        StatusDropdownItems.Children.Clear();
    }

    private void PositionStatusDropdown()
    {
        if (!StatusDropdownLayer.IsVisible)
            return;

        var width = StatusField.Width > 1 ? StatusField.Width : StatusControlWidth;
        width = Math.Max(160, width);
        StatusDropdown.WidthRequest = width;

#if WINDOWS
        if (StatusField.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement field
            && Root.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement root)
        {
            var point = field.TransformToVisual(root)
                .TransformPoint(new Windows.Foundation.Point(0, 0));
            var height = field.ActualHeight > 0 ? field.ActualHeight : StatusField.Height;
            if (field.ActualWidth > 0)
                StatusDropdown.WidthRequest = Math.Max(160, field.ActualWidth);
            StatusDropdown.Margin = new Thickness(point.X, point.Y + height + 4, 0, 0);
            return;
        }
#endif

        double x = 0;
        double y = 0;
        VisualElement? current = StatusField;
        while (current is not null && !ReferenceEquals(current, Root))
        {
            x += current.X;
            y += current.Y;
            if (current.Parent is Border border)
            {
                x += border.Padding.Left;
                y += border.Padding.Top;
            }

            current = current.Parent as VisualElement;
        }

        var fieldHeight = StatusField.Height > 0 ? StatusField.Height : 42;
        StatusDropdown.Margin = new Thickness(x, y + fieldHeight + 4, 0, 0);
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => MainThread.BeginInvokeOnMainThread(RenderList);

    private void OnListPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CustomersViewModel.IsDeleting))
            MainThread.BeginInvokeOnMainThread(CloseStatusDropdown);

        if (e.PropertyName is nameof(CustomersViewModel.CanUpdate)
            or nameof(CustomersViewModel.CanDelete)
            or nameof(CustomersViewModel.IsDeleting)
            or nameof(CustomersViewModel.ShowList)
            or nameof(CustomersViewModel.CanRunDelete))
        {
            MainThread.BeginInvokeOnMainThread(RenderList);
        }
    }

    private async Task ShowFormAsync(Guid? id)
    {
        CloseStatusDropdown();
        ListPanel.IsVisible = false;
        DetailsView.IsVisible = false;
        FormView.IsVisible = true;
        await _form.InitializeAsync(id);
    }

    private async Task ShowDetailsAsync(Guid id)
    {
        CloseStatusDropdown();
        ListPanel.IsVisible = false;
        FormView.IsVisible = false;
        DetailsView.IsVisible = true;
        await _details.LoadAsync(id);
    }

    private void ShowList(bool reload)
    {
        FormView.IsVisible = false;
        DetailsView.IsVisible = false;
        ListPanel.IsVisible = true;
        if (reload)
            _ = _list.LoadAsync();
    }

    private void RenderList()
    {
        ListHost.Children.Clear();
        if (!_list.ShowList)
            return;

        if (_isCompact)
        {
            foreach (var customer in _list.Items)
                ListHost.Children.Add(BuildCard(customer));
            return;
        }

        ListHost.Children.Add(BuildTableHeader());
        foreach (var customer in _list.Items)
            ListHost.Children.Add(BuildTableRow(customer));
    }

    private View BuildTableHeader()
    {
        var grid = CreateRowGrid();
        AddHeader(grid, "Name", 0);
        AddHeader(grid, "Email", 1);
        AddHeader(grid, "Mobile", 2);
        AddHeader(grid, "Status", 3);
        AddHeader(grid, "Actions", 4);

        return new Border
        {
            Style = TryStyle("ListRow"),
            Padding = new Thickness(16, 10),
            Content = grid
        };
    }

    private View BuildTableRow(CustomerModel customer)
    {
        var grid = CreateRowGrid();
        AddCell(grid, customer.Name, 0, bold: true);
        AddCell(grid, Display(customer.Email), 1);
        AddCell(grid, Display(customer.Phone), 2);
        grid.Add(BuildStatusBadge(customer), 3);
        grid.Add(BuildActions(customer), 4);

        return new Border
        {
            Style = TryStyle("ListRow"),
            Content = grid
        };
    }

    private View BuildCard(CustomerModel customer)
    {
        var info = new VerticalStackLayout { Spacing = 6 };
        info.Children.Add(new Label
        {
            Text = customer.Name,
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            LineBreakMode = LineBreakMode.TailTruncation,
            TextColor = Res("TextPrimary", Colors.Black)
        });
        info.Children.Add(Labeled("Email", Display(customer.Email)));
        info.Children.Add(Labeled("Mobile", Display(customer.Phone)));
        info.Children.Add(BuildStatusBadge(customer));
        info.Children.Add(BuildActions(customer));

        return new Border
        {
            Style = TryStyle("ListRow"),
            Content = info
        };
    }

    private static Grid CreateRowGrid()
        => new()
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(new GridLength(1.4, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.5, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.1, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(0.8, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.8, GridUnitType.Star))
            ],
            ColumnSpacing = 8,
            VerticalOptions = LayoutOptions.Center
        };

    private View BuildActions(CustomerModel customer)
    {
        var row = new HorizontalStackLayout
        {
            Spacing = 6,
            HorizontalOptions = LayoutOptions.Start
        };

        var viewBtn = new Button
        {
            Text = "View",
            Style = TryStyle("SmallSecondaryButton"),
            IsEnabled = !_list.IsDeleting && !_list.IsLoading
        };
        viewBtn.Clicked += (_, _) => _list.ViewCommand.Execute(customer);
        row.Children.Add(viewBtn);

        if (_list.CanUpdate)
        {
            var editBtn = new Button
            {
                Text = "Edit",
                Style = TryStyle("SmallButton"),
                IsEnabled = !_list.IsDeleting && !_list.IsLoading
            };
            editBtn.Clicked += (_, _) => _list.EditCommand.Execute(customer);
            row.Children.Add(editBtn);
        }

        if (_list.CanDelete)
        {
            var deleteHost = new Grid
            {
                HeightRequest = 36,
                VerticalOptions = LayoutOptions.Center
            };
            var deleteBtn = new Button
            {
                Text = _list.IsDeleting ? "Deleting..." : "Delete",
                Style = TryStyle("DangerButton"),
                HeightRequest = 36,
                MinimumHeightRequest = 36,
                FontSize = 12,
                Padding = new Thickness(12, 0),
                IsEnabled = _list.CanRunDelete
            };
            deleteBtn.Clicked += (_, _) => _ = _list.DeleteCommand.ExecuteAsync(customer);
            deleteHost.Children.Add(deleteBtn);
            if (_list.IsDeleting)
            {
                deleteHost.Children.Add(new ActivityIndicator
                {
                    IsVisible = true,
                    IsRunning = true,
                    Color = Colors.White,
                    WidthRequest = 14,
                    HeightRequest = 14,
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 0, 8, 0),
                    InputTransparent = true
                });
            }
            row.Children.Add(deleteHost);
        }

        return row;
    }

    private View BuildStatusBadge(CustomerModel customer)
    {
        var (bg, fg) = customer.Status switch
        {
            CustomerStatus.Active => ("SuccessBg", "SuccessText"),
            CustomerStatus.Blocked => ("WarningBg", "WarningText"),
            _ => ("ErrorBg", "ErrorText")
        };

        return new Border
        {
            Style = TryStyle("BadgeBase"),
            BackgroundColor = Res(bg, Colors.Transparent),
            HorizontalOptions = LayoutOptions.Start,
            Content = new Label
            {
                Text = customer.StatusLabel,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Res(fg, Colors.Black)
            }
        };
    }

    private static void AddHeader(Grid grid, string text, int column)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.TailTruncation,
            TextColor = Res("TextSecondary", Colors.Gray)
        };
        grid.Add(label, column, 0);
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
            LineBreakMode = LineBreakMode.TailTruncation,
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
