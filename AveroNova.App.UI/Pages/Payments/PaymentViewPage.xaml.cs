using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Payments;

[QueryProperty(nameof(PaymentId), "id")]
public partial class PaymentViewPage : ContentPage, IHostedPage
{
    private readonly IPaymentService _svc;
    private readonly IMainContentNavigator _navigator;
    private readonly IServiceProvider _services;
    private readonly ISettingsService _settings;
    private PaymentModel? _payment;
    private string _currencySymbol = "₹";
    public string? PaymentId { get; set; }

    public PaymentViewPage(
        IPaymentService svc,
        IMainContentNavigator navigator,
        IServiceProvider services,
        ISettingsService settings)
    {
        InitializeComponent();
        _svc = svc;
        _navigator = navigator;
        _services = services;
        _settings = settings;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadForHostAsync();
    }

    public async Task LoadForHostAsync()
    {
        try
        {
            var appSettings = await _settings.GetAsync();
            _currencySymbol = string.IsNullOrWhiteSpace(appSettings.CurrencySymbol)
                ? (appSettings.Currency == "INR" ? "₹" : "$")
                : appSettings.CurrencySymbol;

            if (!string.IsNullOrEmpty(PaymentId) && Guid.TryParse(PaymentId, out var id))
            {
                _payment = await _svc.GetByIdAsync(id);
                if (_payment != null) BuildContent(_payment);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PaymentView] Load failed: {ex}");
            Content.Children.Clear();
            Content.Children.Add(new Label
            {
                Text = "Payment details could not be loaded.",
                TextColor = Color.FromArgb("#DC2626"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 32)
            });
        }
    }

    private void BuildContent(PaymentModel p)
    {
        Content.Children.Clear();
        var card = new Border
        {
            Stroke = Color.FromArgb("#E2E8F0"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Padding = new Thickness(16)
        };
        var vsl = new VerticalStackLayout { Spacing = 12 };

        vsl.Children.Add(new Label
        {
            Text = $"{_currencySymbol}{p.Amount:N2}",
            FontSize = 32,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#059669"),
            HorizontalOptions = LayoutOptions.Center
        });
        vsl.Children.Add(new Label
        {
            Text = p.PaymentNumber,
            FontSize = 14,
            TextColor = Color.FromArgb("#64748B"),
            HorizontalOptions = LayoutOptions.Center
        });
        vsl.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#E2E8F0") });

        void Row(string label, string value)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection(
                    new ColumnDefinition(new GridLength(130)),
                    new ColumnDefinition(GridLength.Star))
            };
            grid.Add(new Label { Text = label, FontSize = 13, TextColor = Color.FromArgb("#64748B") }, 0, 0);
            grid.Add(new Label { Text = value, FontSize = 13, FontAttributes = FontAttributes.Bold }, 1, 0);
            vsl.Children.Add(grid);
        }

        Row("Party", p.PartyName);
        Row("Invoice", p.InvoiceNumber);
        Row("Date", p.PaymentDate.ToString("dd MMM yyyy"));
        Row("Method", p.MethodLabel);
        Row("Reference", p.Reference);
        Row("Status", p.StatusLabel);
        if (!string.IsNullOrEmpty(p.Notes)) Row("Notes", p.Notes);

        card.Content = vsl;
        Content.Children.Add(card);
    }

    private async void OnDeleteClicked(object? s, EventArgs e)
    {
        if (_payment == null) return;
        try
        {
            if (!await DialogHelper.ConfirmDeleteAsync("Payment", $"Delete {_payment.PaymentNumber}?")) return;
            var result = await _svc.DeleteAsync(_payment.LocalId);
            if (!result.Ok)
            {
                await DisplayAlert("Payment", result.Error ?? "Delete failed.", "OK");
                return;
            }
            await _navigator.GoBackAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PaymentView] Delete failed: {ex}");
            await DisplayAlert("Payment", "Payment could not be deleted.", "OK");
        }
    }

    private async void OnEditClicked(object? s, EventArgs e)
    {
        if (_payment is null) return;
        try
        {
            var page = ActivatorUtilities.CreateInstance<PaymentFormPage>(_services);
            page.EditId = _payment.LocalId.ToString("D");
            await _navigator.NavigateAsync(page, "Edit Payment", "Home / Payments / Edit");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PaymentView] Edit failed: {ex}");
            await DisplayAlert("Payment", "Payment could not be opened for editing.", "OK");
        }
    }

    private async void OnBackClicked(object? s, EventArgs e)
    {
        try { await _navigator.GoBackAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PaymentView] Back failed: {ex}"); }
    }
}
