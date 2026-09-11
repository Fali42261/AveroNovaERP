using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using System.Net;
using System.Text;

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
        var print = MakeButton("Print", false);
        print.Clicked += async (_,_) => await PrintInvoiceAsync(inv);
        actions.Children.Add(print);

        if (inv.Status != InvoiceStatus.Paid && inv.Status != InvoiceStatus.Cancelled)
        {
            var paid = MakeButton("Mark Paid", true);
            paid.Clicked += async (_,_) => await MarkPaidAsync();
            var cancel = MakeButton("Cancel", false);
            cancel.Clicked += async (_,_) => await CancelAsync();
            actions.Children.Add(paid);
            actions.Children.Add(cancel);

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
        }
        Content.Children.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never, Content = actions });

        var statusText = inv.Status switch { InvoiceStatus.Paid => "Completed", InvoiceStatus.Cancelled => "Cancelled", _ => "Pending" };
        var statusColor = inv.Status switch { InvoiceStatus.Paid => "#059669", InvoiceStatus.Cancelled => "#6B7280", _ => "#D97706" };
        var header = new Border { Stroke = Color.FromArgb("#E2E8F0"), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) }, Padding = new Thickness(16) };
        var hv = new VerticalStackLayout { Spacing = 6 };
        hv.Children.Add(new Label { Text = inv.InvoiceNumber, FontSize = 20, FontAttributes = FontAttributes.Bold });
        hv.Children.Add(new Label { Text = inv.CustomerName, FontSize = 14, TextColor = Color.FromArgb("#64748B") });
        hv.Children.Add(new Label { Text = statusText, FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(statusColor) });
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

    private async Task PrintInvoiceAsync(InvoiceModel inv)
    {
        try
        {
            var html = BuildInvoiceHtml(inv);
#if ANDROID
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (activity is null) throw new InvalidOperationException("Android activity is unavailable.");
            var manager = activity.GetSystemService(Android.Content.Context.PrintService) as Android.Print.PrintManager;
            if (manager is null) throw new InvalidOperationException("Print service is unavailable.");
            var webView = new Android.Webkit.WebView(activity);
            webView.SetWebViewClient(new InvoicePrintClient(() =>
            {
                var adapter = webView.CreatePrintDocumentAdapter($"Invoice-{inv.InvoiceNumber}");
                manager.Print($"Invoice {inv.InvoiceNumber}", adapter, new Android.Print.PrintAttributes.Builder().Build());
            }));
            webView.LoadDataWithBaseURL(null, html, "text/html", "UTF-8", null);
#else
            var path = Path.Combine(FileSystem.CacheDirectory, $"Invoice-{inv.InvoiceNumber}.html");
            await File.WriteAllTextAsync(path, html);
            await Share.Default.RequestAsync(new ShareFileRequest { Title = $"Invoice {inv.InvoiceNumber}", File = new ShareFile(path) });
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InvoiceView] Print failed: {ex}");
            await DisplayAlert("Print", "Invoice could not be opened for printing.", "OK");
        }
    }

    private string BuildInvoiceHtml(InvoiceModel inv)
    {
        var sb = new StringBuilder();
        sb.Append("<html><head><meta charset='utf-8'><style>body{font-family:sans-serif;padding:24px;color:#0f172a}h1{margin:0}.meta{color:#64748b}.row{display:flex;justify-content:space-between;padding:8px 0;border-bottom:1px solid #e2e8f0}.total{font-size:20px;font-weight:700;margin-top:18px}</style></head><body>");
        sb.Append($"<h1>SwapDigit Invoice</h1><div class='meta'>{WebUtility.HtmlEncode(inv.InvoiceNumber)} · {WebUtility.HtmlEncode(inv.InvoiceDate.ToString(_dateFormat))}</div>");
        sb.Append($"<h3>Customer: {WebUtility.HtmlEncode(inv.CustomerName)}</h3>");
        foreach (var item in inv.Items)
            sb.Append($"<div class='row'><span>{WebUtility.HtmlEncode(item.ProductName)} × {item.Quantity}</span><strong>{WebUtility.HtmlEncode(Money(item.GrandTotal))}</strong></div>");
        sb.Append($"<div class='total'>Total: {WebUtility.HtmlEncode(Money(inv.GrandTotal))}</div>");
        sb.Append($"<p>Status: {(inv.Status == InvoiceStatus.Paid ? "Completed" : inv.Status == InvoiceStatus.Cancelled ? "Cancelled" : "Pending")}</p>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

#if ANDROID
    private sealed class InvoicePrintClient(Action onReady) : Android.Webkit.WebViewClient
    {
        private bool _printed;
        public override void OnPageFinished(Android.Webkit.WebView? view, string? url)
        {
            base.OnPageFinished(view, url);
            if (_printed) return;
            _printed = true;
            onReady();
        }
    }
#endif

    private async Task MarkPaidAsync()
    {
        if (_invoice is null) return;
        if (!await DialogHelper.ConfirmAsync("Mark Paid", "Mark this invoice as fully paid and completed?", "Mark Paid", "Back")) return;
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
