using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Billing;

public partial class BillingListPage : ContentPage, IHostedPage
{
    private readonly IBillingService _svc;
    private readonly ICompanyService _company;
    private readonly ISettingsService _settings;
    private readonly IMainContentNavigator _navigator;
    private readonly IServiceProvider _services;
    private readonly Dictionary<string, Button> _filterButtons = new(StringComparer.OrdinalIgnoreCase);
    private List<InvoiceModel> _all = [];
    private string _filter = "All";
    private string _currencySymbol = "₹";
    private string _dateFormat = "dd MMM yyyy";
    private bool _loading;

    public BillingListPage(IBillingService svc, ICompanyService company, ISettingsService settings, IMainContentNavigator navigator, IServiceProvider services)
    {
        InitializeComponent();
        _svc = svc; _company = company; _settings = settings; _navigator = navigator; _services = services;
        BuildFilterTabs();
    }

    protected override async void OnAppearing() { base.OnAppearing(); await LoadForHostAsync(); }

    public async Task LoadForHostAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            var appSettings = await _settings.GetAsync();
            _currencySymbol = string.IsNullOrWhiteSpace(appSettings.CurrencySymbol) ? (appSettings.Currency == "INR" ? "₹" : "$") : appSettings.CurrencySymbol;
            _dateFormat = string.IsNullOrWhiteSpace(appSettings.DateFormat) ? AppRegionalPreferences.DateFormat : appSettings.DateFormat;
            var companyId = _company.CurrentCompany?.LocalId ?? Guid.Empty;
            _all = companyId == Guid.Empty ? [] : await _svc.GetAllAsync(companyId);
            RenderCurrentFilter();
            UpdateFilterVisuals();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Billing] Load failed: {ex}");
            InvoiceList.Children.Clear();
            InvoiceList.Children.Add(new Label { Text = "Billing could not be loaded. Pull down to retry.", TextColor = Color.FromArgb("#DC2626"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0,32) });
        }
        finally { _loading = false; }
    }

    private async void OnRefreshing(object? sender, EventArgs e) { try { await LoadForHostAsync(); } finally { Refresher.IsRefreshing = false; } }

    private void BuildFilterTabs()
    {
        FilterTabs.Children.Clear();
        _filterButtons.Clear();
        foreach (var status in new[] { "All", "Draft", "Paid", "Cancelled" })
        {
            var button = new Button { Text = status, FontSize = 12, HeightRequest = 36, MinimumWidthRequest = 72, Padding = new Thickness(14,0), CornerRadius = 18, BorderWidth = 1 };
            var captured = status;
            button.Clicked += (_,_) => { _filter = captured; UpdateFilterVisuals(); RenderCurrentFilter(); };
            _filterButtons[status] = button;
            FilterTabs.Children.Add(button);
        }
        UpdateFilterVisuals();
    }

    private void UpdateFilterVisuals()
    {
        foreach (var pair in _filterButtons)
        {
            var selected = string.Equals(pair.Key, _filter, StringComparison.OrdinalIgnoreCase);
            pair.Value.BackgroundColor = selected ? Color.FromArgb("#2563EB") : Colors.Transparent;
            pair.Value.TextColor = selected ? Colors.White : Color.FromArgb("#64748B");
            pair.Value.BorderColor = selected ? Color.FromArgb("#2563EB") : Color.FromArgb("#CBD5E1");
            pair.Value.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
        }
    }

    private void RenderCurrentFilter()
    {
        var shown = _filter switch
        {
            "Draft" => _all.Where(i => i.Status == InvoiceStatus.Draft).ToList(),
            "Paid" => _all.Where(i => i.Status == InvoiceStatus.Paid).ToList(),
            "Cancelled" => _all.Where(i => i.Status == InvoiceStatus.Cancelled).ToList(),
            _ => _all.ToList()
        };
        LblCount.Text = $"{shown.Count} invoice{(shown.Count == 1 ? string.Empty : "s")}";
        InvoiceList.Children.Clear();
        if (shown.Count == 0)
        {
            InvoiceList.Children.Add(new Label { Text = _filter == "All" ? "No invoices found." : $"No {_filter.ToLowerInvariant()} invoices found.", FontSize = 14, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0,40) });
            return;
        }
        foreach (var inv in shown.OrderByDescending(i => i.InvoiceDate)) InvoiceList.Children.Add(BuildRow(inv));
    }

    private View BuildRow(InvoiceModel inv)
    {
        var (statusBg,statusColor) = inv.Status switch
        {
            InvoiceStatus.Paid => ("#ECFDF5","#059669"),
            InvoiceStatus.Cancelled => ("#F3F4F6","#6B7280"),
            _ => ("#FFFBEB","#D97706")
        };
        var border = new Border { Stroke = Color.FromArgb("#E2E8F0"), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) }, Padding = new Thickness(14,12) };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12 };
        var left = new VerticalStackLayout { Spacing = 4 };
        left.Children.Add(new Label { Text = inv.InvoiceNumber, FontSize = 14, FontAttributes = FontAttributes.Bold });
        left.Children.Add(new Label { Text = inv.CustomerName, FontSize = 13, TextColor = Color.FromArgb("#64748B") });
        left.Children.Add(new Label { Text = $"{inv.InvoiceDate.ToString(_dateFormat)}  •  Due: {inv.DueDate.ToString(_dateFormat)}", FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });
        var right = new VerticalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label { Text = Money(inv.GrandTotal), FontSize = 15, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.End });
        right.Children.Add(new Border { BackgroundColor = Color.FromArgb(statusBg), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) }, Padding = new Thickness(8,3), Content = new Label { Text = inv.StatusLabel, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(statusColor) } });
        var view = new Button { Text = "View", FontSize = 12, HeightRequest = 36, Padding = new Thickness(12,0), CornerRadius = 8, BackgroundColor = Colors.Transparent, TextColor = Color.FromArgb("#2563EB"), BorderColor = Color.FromArgb("#2563EB"), BorderWidth = 1 };
        view.Clicked += async (_,_) => await OpenInvoiceAsync(inv);
        right.Children.Add(view);
        grid.Add(left,0,0); grid.Add(right,1,0); border.Content = grid;
        return border;
    }

    private async Task OpenInvoiceAsync(InvoiceModel inv)
    {
        try
        {
            var page = ActivatorUtilities.CreateInstance<InvoiceViewPage>(_services);
            page.InvoiceId = inv.LocalId.ToString("D");
            await _navigator.NavigateAsync(page, "Invoice Details", "Home / Billing / Invoice");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Billing] Invoice view failed: {ex}");
            await DisplayAlert("Billing", "Invoice could not be opened.", "OK");
        }
    }

    private string Money(decimal amount) => $"{_currencySymbol}{amount:N2}";

    private async void OnNewClicked(object? sender, EventArgs e)
    {
        try
        {
            var page = ActivatorUtilities.CreateInstance<InvoiceFormPage>(_services);
            await _navigator.NavigateAsync(page, "New Invoice", "Home / Billing / New Invoice");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Billing] New invoice navigation failed: {ex}");
            await DisplayAlert("Billing", "New invoice could not be opened.", "OK");
        }
    }
}
