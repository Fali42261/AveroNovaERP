using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Billing;

[QueryProperty(nameof(InvoiceId), "id")]
public partial class InvoiceViewPage : ContentPage, IHostedPage
{
    private readonly IBillingService _svc;
    private readonly IMainContentNavigator _navigator;
    private readonly IServiceProvider _services;
    private readonly ISettingsService _settings;
    private InvoiceModel? _invoice;
    private string _currencySymbol = "₹";
    private string _dateFormat = "dd MMM yyyy";
    public string? InvoiceId { get; set; }

    public InvoiceViewPage(IBillingService svc, IMainContentNavigator navigator, IServiceProvider services, ISettingsService settings)
    {
        InitializeComponent();
        _svc = svc; _navigator = navigator; _services = services; _settings = settings;
    }

    protected override async void OnAppearing() { base.OnAppearing(); await LoadForHostAsync(); }

    public async Task LoadForHostAsync()
    {
        try
        {
            var s = await _settings.GetAsync();
            _currencySymbol = string.IsNullOrWhiteSpace(s.CurrencySymbol) ? (s.Currency == "INR" ? "₹" : "$") : s.CurrencySymbol;
            _dateFormat = string.IsNullOrWhiteSpace(s.DateFormat) ? "dd MMM yyyy" : s.DateFormat;
            if (!string.IsNullOrWhiteSpace(InvoiceId) && Guid.TryParse(InvoiceId, out var id))
            {
                _invoice = await _svc.GetByIdAsync(id);
                if (_invoice is not null) BuildContent(_invoice);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InvoiceView] Load failed: {ex}");
            Content.Children.Clear();
            Content.Children.Add(new Label { Text = "Invoice could not be loaded.", TextColor = Color.FromArgb("#DC2626"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0,32) });
        }
    }

    private void BuildContent(InvoiceModel inv)
    {
        Content.Children.Clear();

        var actions = new HorizontalStackLayout { Spacing = 8 };
        if (inv.Status == InvoiceStatus.Draft)
        {
            var paid = MakeButton("Mark Paid", true);
            paid.Clicked += async (_,_) => await MarkPaidAsync();
            var cancel = MakeButton("Cancel", false);
            cancel.Clicked += async (_,_) => await CancelAsync();
            actions.Children.Add(paid);
            actions.Children.Add(cancel);
        }
        if (inv.Status == InvoiceStatus.Draft)
        {
            var edit = MakeButton("Edit", false);
            edit.Clicked += async (_,_) =>
            {
                try
                {
                    var page = ActivatorUtilities.CreateInstance<InvoiceFormPage>(_services);
                    page.EditId = inv.LocalId.ToString("D");
                    await _navigator.NavigateAsync(page, "Edit Invoice", "Home / Billing / Edit Invoice");
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[InvoiceView] Edit failed: {ex}"); }
            };
            actions.Children.Add(edit);
        }
        Content.Children.Add(actions);

        var header = new Border { Stroke = Color.FromArgb("#E2E8F0"), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) }, Padding = new Thickness(16) };
        var hv = new VerticalStackLayout { Spacing = 6 };
        hv.Children.Add(new Label { Text = inv.InvoiceNumber, FontSize = 20, FontAttributes = FontAttributes.Bold });
        hv.Children.Add(new Label { Text = inv.CustomerName, FontSize = 14, TextColor = Color.FromArgb("#64748B") });
        hv.Children.Add(new Label { Text = inv.StatusLabel, FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = inv.Status == InvoiceStatus.Paid ? Color.FromArgb("#059669") : inv.Status == InvoiceStatus.Cancelled ? Color.FromArgb("#6B7280") : Color.FromArgb("#D97706") });
        header.Content = hv;
        Content.Children.Add(header);

        var amount = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Star)), ColumnSpacing = 8 };
        amount.Add(Stat("Total", Money(inv.GrandTotal)),0,0);
        amount.Add(Stat("Paid", Money(inv.PaidAmount)),1,0);
        amount.Add(Stat("Due", Money(Math.Max(0,inv.DueAmount))),2,0);
        Content.Children.Add(new Border { Stroke = Color.FromArgb("#E2E8F0"), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) }, Padding = new Thickness(16), Content = amount });

        var details = new VerticalStackLayout { Spacing = 10 };
        details.Children.Add(Detail("Invoice Date", inv.InvoiceDate.ToString(_dateFormat)));
        details.Children.Add(Detail("Due Date", inv.DueDate.ToString(_dateFormat)));
        details.Children.Add(Detail("Payment Method", inv.PaymentMethod.ToString()));
        if (!string.IsNullOrWhiteSpace(inv.Notes)) details.Children.Add(Detail("Notes", inv.Notes));
        Content.Children.Add(new Border { Stroke = Color.FromArgb("#E2E8F0"), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) }, Padding = new Thickness(16), Content = details });

        if (inv.Items.Count > 0)
        {
            var lines = new VerticalStackLayout { Spacing = 10 };
            lines.Children.Add(new Label { Text = "Line Items", FontAttributes = FontAttributes.Bold, FontSize = 14 });
            foreach (var item in inv.Items)
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star),new ColumnDefinition(GridLength.Auto)) };
                row.Add(new Label { Text = $"{item.ProductName}  × {item.Quantity}", FontSize = 13 },0,0);
                row.Add(new Label { Text = Money(item.GrandTotal), FontSize = 13, FontAttributes = FontAttributes.Bold },1,0);
                lines.Children.Add(row);
            }
            Content.Children.Add(new Border { Stroke = Color.FromArgb("#E2E8F0"), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) }, Padding = new Thickness(16), Content = lines });
        }
    }

    private async Task MarkPaidAsync()
    {
        if (_invoice is null) return;
        if (!await DialogHelper.ConfirmAsync("Mark Paid", "Mark this invoice as fully paid?", "Mark Paid", "Back")) return;
        var result = await _svc.MarkPaidAsync(_invoice.LocalId);
        if (!result.Ok) { await DisplayAlert("Billing", result.Error ?? "Invoice could not be marked paid.", "OK"); return; }
        await LoadForHostAsync();
    }

    private async Task CancelAsync()
    {
        if (_invoice is null) return;
        if (!await DialogHelper.ConfirmAsync("Cancel Invoice", "Cancel this invoice?", "Cancel Invoice", "Back")) return;
        var result = await _svc.CancelAsync(_invoice.LocalId);
        if (!result.Ok) { await DisplayAlert("Billing", result.Error ?? "Invoice could not be cancelled.", "OK"); return; }
        await LoadForHostAsync();
    }

    private static Button MakeButton(string text, bool primary) => new() { Text = text, HeightRequest = 40, CornerRadius = 8, Padding = new Thickness(14,0), BackgroundColor = primary ? Color.FromArgb("#2563EB") : Colors.Transparent, TextColor = primary ? Colors.White : Color.FromArgb("#2563EB"), BorderColor = Color.FromArgb("#2563EB"), BorderWidth = 1 };
    private string Money(decimal value) => $"{_currencySymbol}{value:N2}";
    private static View Stat(string label, string value) { var s = new VerticalStackLayout { Spacing = 3, HorizontalOptions = LayoutOptions.Center }; s.Children.Add(new Label { Text = value, FontAttributes = FontAttributes.Bold, FontSize = 16, HorizontalOptions = LayoutOptions.Center }); s.Children.Add(new Label { Text = label, FontSize = 11, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center }); return s; }
    private static View Detail(string label,string value) { var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(new GridLength(130)),new ColumnDefinition(GridLength.Star)) }; g.Add(new Label { Text = label, TextColor = Color.FromArgb("#64748B"), FontSize = 13 },0,0); g.Add(new Label { Text = value, FontAttributes = FontAttributes.Bold, FontSize = 13 },1,0); return g; }

    private async void OnBackClicked(object? s, EventArgs e)
    {
        try { await _navigator.GoBackAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[InvoiceView] Back failed: {ex}"); }
    }
}
