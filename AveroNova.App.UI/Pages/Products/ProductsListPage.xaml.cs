using System.Collections.Specialized;
using AveroNova.App.UI.Layout;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Products;

public partial class ProductsListPage : ContentPage
{
    private const double StatusControlWidth = 180;
    private const double ToolbarColumnSpacing = 12;

    private readonly ProductsViewModel _list;
    private readonly ProductFormViewModel _form;
    private readonly ProductViewViewModel _details;
    private bool _isCompact;
    private bool _isExpanded;

    public ProductsListPage(ProductsViewModel list, ProductFormViewModel form, ProductViewViewModel details)
    {
        InitializeComponent();
        _list = list;
        _form = form;
        _details = details;
        BindingContext = list;
        if (Content != null) Content.BindingContext = list;
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
            if (_form.IsEditMode && _form.SavedId != Guid.Empty) { _ = ShowDetailsAsync(_form.SavedId); return; }
            _list.ResetPaging(); ShowList(reload: true);
        };
        _form.Cancelled += (_, _) =>
        {
            if (_form.IsEditMode && _form.EditingId is Guid editId && editId != Guid.Empty) { _ = ShowDetailsAsync(editId); return; }
            ShowList(reload: false);
        };
        _details.BackRequested += (_, _) => ShowList(reload: false);
        _details.EditRequested += (_, id) => _ = ShowFormAsync(id);
        Root.SizeChanged += OnRootSizeChanged;
        ShowList(reload: false);
    }

    public Task ReloadAsync() { ShowList(reload: true); return Task.CompletedTask; }

    protected override async void OnAppearing() { base.OnAppearing(); if (ListPanel.IsVisible) await _list.LoadAsync(); }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await _list.LoadAsync(showLoading: false);
        if (sender is RefreshView refresh) refresh.IsRefreshing = false;
    }

    private void OnRootSizeChanged(object? sender, EventArgs e)
    {
        if (Root.Width <= 0) return;
        var size = ResponsiveBreakpoints.FromWidth(Root.Width);
        var compact = size == ScreenSize.Compact;
        var expanded = size == ScreenSize.Expanded;
        _list.IsCompact = compact;
        ApplyToolbarLayout(compact);
        if (StatusDropdownLayer.IsVisible) PositionStatusDropdown();
        if (compact != _isCompact || expanded != _isExpanded)
        {
            _isCompact = compact; _isExpanded = expanded; RenderList();
        }
    }

    private void ApplyToolbarLayout(bool compact)
    {
        var addButton = PageHeader.Children.OfType<Button>().FirstOrDefault();
        if (compact)
        {
            PageHeader.ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Star) };
            PageHeader.RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            };
            PageHeader.RowSpacing = 8;

            Grid.SetColumn(SearchHost, 0); Grid.SetRow(SearchHost, 0); Grid.SetColumnSpan(SearchHost, 1);
            SearchHost.WidthRequest = -1; SearchHost.MaximumWidthRequest = double.PositiveInfinity; SearchHost.HorizontalOptions = LayoutOptions.Fill;

            Grid.SetColumn(StatusHost, 0); Grid.SetRow(StatusHost, 1); Grid.SetColumnSpan(StatusHost, 1);
            StatusHost.WidthRequest = -1; StatusHost.MinimumWidthRequest = 0; StatusHost.MaximumWidthRequest = double.PositiveInfinity; StatusHost.HorizontalOptions = LayoutOptions.Fill;

            if (addButton != null)
            {
                Grid.SetColumn(addButton, 0); Grid.SetRow(addButton, 2); Grid.SetColumnSpan(addButton, 1);
                addButton.HorizontalOptions = LayoutOptions.Fill;
            }
            return;
        }

        PageHeader.ColumnDefinitions = new ColumnDefinitionCollection
        {
            new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(new GridLength(150)),
            new ColumnDefinition(GridLength.Auto)
        };
        PageHeader.RowDefinitions = new RowDefinitionCollection { new RowDefinition(GridLength.Auto) };
        PageHeader.RowSpacing = 0;

        Grid.SetColumn(SearchHost, 0); Grid.SetRow(SearchHost, 0); Grid.SetColumnSpan(SearchHost, 1);
        SearchHost.HorizontalOptions = LayoutOptions.Start; ApplySearchWidth();

        Grid.SetColumn(StatusHost, 1); Grid.SetRow(StatusHost, 0); Grid.SetColumnSpan(StatusHost, 1);
        StatusHost.WidthRequest = 150; StatusHost.MinimumWidthRequest = 140; StatusHost.MaximumWidthRequest = 180; StatusHost.HorizontalOptions = LayoutOptions.Fill;

        if (addButton != null)
        {
            Grid.SetColumn(addButton, 2); Grid.SetRow(addButton, 0); Grid.SetColumnSpan(addButton, 1);
            addButton.HorizontalOptions = LayoutOptions.End;
        }
    }

    private void ApplySearchWidth()
    {
        var innerWidth = Root.Width > 1 ? Root.Width : 0;
        if (innerWidth <= 0) return;
        var reserved = StatusControlWidth + ToolbarColumnSpacing + 150;
        var maxFit = Math.Max(200, innerWidth - reserved);
        var upper = Math.Max(200, Math.Min(innerWidth * 0.65, maxFit));
        var lower = Math.Min(240, upper);
        var searchWidth = Math.Clamp(innerWidth * 0.60, lower, upper);
        SearchHost.WidthRequest = searchWidth; SearchHost.MaximumWidthRequest = innerWidth * 0.65;
    }

    private void OnStatusFieldTapped(object? sender, TappedEventArgs e)
    {
        if (_list.IsDeleting) return;
        if (StatusDropdownLayer.IsVisible) { CloseStatusDropdown(); return; }
        OpenStatusDropdown();
    }

    private void OnStatusDropdownDismissed(object? sender, TappedEventArgs e) => CloseStatusDropdown();

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
            row.SetAppThemeColor(Border.BackgroundColorProperty,
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

    private void SelectStatusFilter(string option) { _list.SelectedStatusFilter = option; CloseStatusDropdown(); }
    private void CloseStatusDropdown() { if (!StatusDropdownLayer.IsVisible) return; StatusDropdownLayer.IsVisible = false; StatusDropdownItems.Children.Clear(); }

    private void PositionStatusDropdown()
    {
        if (!StatusDropdownLayer.IsVisible) return;
        var width = StatusHost.Width > 1 ? StatusHost.Width : StatusControlWidth;
        width = Math.Max(160, width);
        StatusDropdown.WidthRequest = width;
#if WINDOWS
        if (StatusHost.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement field && Root.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement root)
        {
            var point = field.TransformToVisual(root).TransformPoint(new Windows.Foundation.Point(0, 0));
            var height = field.ActualHeight > 0 ? field.ActualHeight : StatusHost.Height;
            if (field.ActualWidth > 0) StatusDropdown.WidthRequest = Math.Max(160, field.ActualWidth);
            StatusDropdown.Margin = new Thickness(point.X, point.Y + height + 4, 0, 0);
            return;
        }
#endif
        double x = 0, y = 0;
        VisualElement? current = StatusHost;
        while (current is not null && !ReferenceEquals(current, Root))
        {
            x += current.X; y += current.Y;
            if (current.Parent is Border border) { x += border.Padding.Left; y += border.Padding.Top; }
            current = current.Parent as VisualElement;
        }
        var fieldHeight = StatusHost.Height > 0 ? StatusHost.Height : 42;
        StatusDropdown.Margin = new Thickness(x, y + fieldHeight + 4, 0, 0);
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => MainThread.BeginInvokeOnMainThread(RenderList);

    private void OnListPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProductsViewModel.IsDeleting)) MainThread.BeginInvokeOnMainThread(CloseStatusDropdown);
        if (e.PropertyName is nameof(ProductsViewModel.CanUpdate) or nameof(ProductsViewModel.CanDelete) or nameof(ProductsViewModel.IsDeleting) or nameof(ProductsViewModel.ShowList) or nameof(ProductsViewModel.CanRunDelete)) MainThread.BeginInvokeOnMainThread(RenderList);
    }

    private async Task ShowFormAsync(Guid? id)
    {
        var isEdit = id is Guid editId && editId != Guid.Empty;
        if (isEdit) { if (!_list.CanUpdate) return; } else if (!_list.CanCreate) return;
        CloseStatusDropdown();
        ListPanel.IsVisible = false;
        DetailsView.IsVisible = false;
        FormView.IsVisible = true;
        await _form.InitializeAsync(id);
    }

    private async Task ShowDetailsAsync(Guid id)
    {
        if (!_list.CanView) return;
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
        if (reload) _ = _list.LoadAsync();
    }

    private void RenderList()
    {
        if (!ListPanel.IsVisible || ListHost == null) return;
        ListHost.Children.Clear();
        foreach (var product in _list.Items) ListHost.Children.Add(BuildProductRow(product));
    }

    private View BuildProductRow(ProductModel product)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)),
            Padding = new Thickness(16, 12),
            ColumnSpacing = 12
        };
        var left = new VerticalStackLayout { Spacing = 2 };
        left.Children.Add(new Label { Text = product.Name, FontSize = 13, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.TailTruncation });
        left.Children.Add(new Label { Text = string.IsNullOrWhiteSpace(product.SKU) ? "No SKU" : product.SKU, FontSize = 11, TextColor = Res("TextSecondary", Color.FromArgb("#64748B")) });
        var right = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label { Text = $"{product.SellingPrice:N2}", FontSize = 13, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center });
        right.Children.Add(new Label { Text = $"Stock {product.Stock}", FontSize = 11, TextColor = Res("TextSecondary", Color.FromArgb("#64748B")), VerticalOptions = LayoutOptions.Center });
        grid.Add(left, 0, 0);
        grid.Add(right, 1, 0);
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await ShowDetailsAsync(product.LocalId);
        grid.GestureRecognizers.Add(tap);
        return grid;
    }

    private static Color Res(string key, Color fallback)
        => Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color ? color : fallback;
}
