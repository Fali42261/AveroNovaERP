using System.Collections.Specialized;
using AveroNova.App.UI.Layout;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Products;

public partial class ProductsListPage : ContentPage
{
    private const double StatusControlWidth = 180;
    private const double ToolbarGutter = 32;
    private const double ToolbarColumnSpacing = 12;

    private readonly ProductsViewModel _list;
    private readonly ProductFormViewModel _form;
    private readonly ProductViewViewModel _details;
    private bool _isCompact;
    private bool _isExpanded;

    public ProductsListPage(
        ProductsViewModel list,
        ProductFormViewModel form,
        ProductViewViewModel details)
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
        _list.ViewRequested += (_, product) => _ = ShowDetailsAsync(product.LocalId);
        _list.EditRequested += (_, product) => _ = ShowFormAsync(product.LocalId);
        _list.Items.CollectionChanged += OnItemsChanged;
        _list.PropertyChanged += OnListPropertyChanged;

        _form.Saved += (_, _) =>
        {
            if (_form.IsEditMode && _form.SavedId != Guid.Empty)
            {
                _ = ShowDetailsAsync(_form.SavedId);
                return;
            }

            _list.ResetPaging();
            ShowList(reload: true);
        };
        _form.Cancelled += (_, _) =>
        {
            if (_form.IsEditMode && _form.EditingId is Guid editId && editId != Guid.Empty)
            {
                _ = ShowDetailsAsync(editId);
                return;
            }

            ShowList(reload: false);
        };
        _details.BackRequested += (_, _) => ShowList(reload: false);
        _details.EditRequested += (_, id) => _ = ShowFormAsync(id);

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
        ApplyHeaderLayout(compact);
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

    private void ApplyHeaderLayout(bool compact)
    {
        if (compact)
        {
            PageHeader.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star)
            };
            PageHeader.RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            };
            Grid.SetColumn(HeaderTitle, 0);
            Grid.SetRow(HeaderTitle, 0);
            Grid.SetColumn(ActionHost, 0);
            Grid.SetRow(ActionHost, 1);
            ActionHost.HorizontalOptions = LayoutOptions.Start;
            return;
        }

        PageHeader.ColumnDefinitions = new ColumnDefinitionCollection
        {
            new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(GridLength.Auto)
        };
        PageHeader.RowDefinitions = new RowDefinitionCollection
        {
            new RowDefinition(GridLength.Auto)
        };
        Grid.SetColumn(HeaderTitle, 0);
        Grid.SetRow(HeaderTitle, 0);
        Grid.SetColumn(ActionHost, 1);
        Grid.SetRow(ActionHost, 0);
        ActionHost.HorizontalOptions = LayoutOptions.End;
    }

    private void ApplyToolbarLayout(bool compact)
    {
        if (compact)
        {
            Grid.SetColumn(SearchHost, 0);
            Grid.SetRow(SearchHost, 0);
            Grid.SetColumnSpan(SearchHost, 2);
            SearchHost.WidthRequest = -1;
            SearchHost.MaximumWidthRequest = double.PositiveInfinity;
            SearchHost.HorizontalOptions = LayoutOptions.Fill;

            Grid.SetColumn(StatusHost, 0);
            Grid.SetRow(StatusHost, 1);
            Grid.SetColumnSpan(StatusHost, 2);
            StatusHost.WidthRequest = -1;
            StatusHost.MinimumWidthRequest = 0;
            StatusHost.MaximumWidthRequest = double.PositiveInfinity;
            StatusHost.HorizontalOptions = LayoutOptions.Fill;
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
    }

    private void ApplySearchWidth()
    {
        var innerWidth = ToolbarGrid.Width > 1
            ? ToolbarGrid.Width
            : Math.Max(Root.Width - ToolbarGutter, 0);
        if (innerWidth <= 0)
            return;

        var reserved = StatusControlWidth + ToolbarColumnSpacing;
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
        if (e.PropertyName is nameof(ProductsViewModel.IsDeleting))
            MainThread.BeginInvokeOnMainThread(CloseStatusDropdown);

        if (e.PropertyName is nameof(ProductsViewModel.CanUpdate)
            or nameof(ProductsViewModel.CanDelete)
            or nameof(ProductsViewModel.IsDeleting)
            or nameof(ProductsViewModel.ShowList)
            or nameof(ProductsViewModel.CanRunDelete))
        {
            MainThread.BeginInvokeOnMainThread(RenderList);
        }
    }

    private async Task ShowFormAsync(Guid? id)
    {
        var isEdit = id is Guid editId && editId != Guid.Empty;
        if (isEdit)
        {
            if (!_list.CanUpdate)
                return;
        }
        else if (!_list.CanCreate)
        {
            return;
        }

        CloseStatusDropdown();
        ListPanel.IsVisible = false;
        DetailsView.IsVisible = false;
        FormView.IsVisible = true;
        await _form.InitializeAsync(id);
    }

    private async Task ShowDetailsAsync(Guid id)
    {
        if (!_list.CanView)
            return;

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
            foreach (var product in _list.Items)
                ListHost.Children.Add(BuildCard(product));
            return;
        }

        ListHost.Children.Add(BuildTableHeader());
        foreach (var product in _list.Items)
            ListHost.Children.Add(BuildTableRow(product));
    }

    private View BuildTableHeader()
    {
        var grid = CreateRowGrid();
        AddHeader(grid, "Product Name", 0);
        AddHeader(grid, "SKU", 1);
        AddHeader(grid, "Category", 2);
        AddHeader(grid, "Unit", 3);
        AddHeader(grid, "Sale Price", 4);
        AddHeader(grid, "Stock", 5);
        AddHeader(grid, "Status", 6);
        AddHeader(grid, "Actions", 7);

        return new Border
        {
            Style = TryStyle("ListRow"),
            Padding = new Thickness(16, 10),
            Content = grid
        };
    }

    private View BuildTableRow(ProductModel product)
    {
        var grid = CreateRowGrid();
        AddCell(grid, product.Name, 0, bold: true);
        AddCell(grid, Display(product.SKU), 1);
        AddCell(grid, Display(product.Category), 2);
        AddCell(grid, Display(product.Unit), 3);
        AddCell(grid, $"₹{product.SellingPrice:N2}", 4);
        AddCell(grid, product.Stock.ToString(), 5);
        grid.Add(BuildStatusBadge(product), 6);
        grid.Add(BuildActions(product), 7);

        return new Border
        {
            Style = TryStyle("ListRow"),
            Content = grid
        };
    }

    private View BuildCard(ProductModel product)
    {
        var info = new VerticalStackLayout { Spacing = 6 };
        info.Children.Add(new Label
        {
            Text = product.Name,
            FontAttributes = FontAttributes.Bold,
            FontSize = 14,
            MaxLines = 1,
            LineBreakMode = LineBreakMode.TailTruncation,
            TextColor = Res("TextPrimary", Colors.Black)
        });
        info.Children.Add(Labeled("SKU", Display(product.SKU)));
        info.Children.Add(Labeled("Category", Display(product.Category)));
        info.Children.Add(Labeled("Unit", Display(product.Unit)));
        info.Children.Add(Labeled("Sale Price", $"₹{product.SellingPrice:N2}"));
        info.Children.Add(Labeled("Stock", product.Stock.ToString()));
        info.Children.Add(BuildStatusBadge(product));
        info.Children.Add(BuildActions(product));

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
                new ColumnDefinition(new GridLength(1.0, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.0, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(0.6, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(0.9, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(0.6, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(0.8, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.8, GridUnitType.Star))
            ],
            ColumnSpacing = 8,
            VerticalOptions = LayoutOptions.Center
        };

    private View BuildActions(ProductModel product)
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
        viewBtn.Clicked += (_, _) => _list.ViewCommand.Execute(product);
        row.Children.Add(viewBtn);

        if (_list.CanUpdate)
        {
            var editBtn = new Button
            {
                Text = "Edit",
                Style = TryStyle("SmallButton"),
                IsEnabled = !_list.IsDeleting && !_list.IsLoading
            };
            editBtn.Clicked += (_, _) => _list.EditCommand.Execute(product);
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
            deleteBtn.Clicked += (_, _) => _ = _list.DeleteCommand.ExecuteAsync(product);
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

    private View BuildStatusBadge(ProductModel product)
    {
        var (bg, fg) = product.Status switch
        {
            ProductStatus.Active => ("SuccessBg", "SuccessText"),
            ProductStatus.Inactive => ("ErrorBg", "ErrorText"),
            _ => ("WarningBg", "WarningText")
        };

        return new Border
        {
            Style = TryStyle("BadgeBase"),
            BackgroundColor = Res(bg, Colors.Transparent),
            HorizontalOptions = LayoutOptions.Start,
            Content = new Label
            {
                Text = product.StatusLabel,
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
