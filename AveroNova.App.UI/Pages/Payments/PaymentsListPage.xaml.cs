using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Payments;

public partial class PaymentsListPage : ContentPage, IHostedPage
{
    private readonly IPaymentService _svc;
    private readonly ICompanyService _company;
    private readonly IMainContentNavigator _navigator;
    private readonly IServiceProvider _services;
    private readonly ISettingsService _settings;
    private string _currencySymbol = "₹";
    private bool _loading;

    public PaymentsListPage(
        IPaymentService svc,
        ICompanyService company,
        IMainContentNavigator navigator,
        IServiceProvider services,
        ISettingsService settings)
    {
        InitializeComponent();
        _svc = svc;
        _company = company;
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
        if (_loading) return;
        _loading = true;
        try
        {
            var appSettings = await _settings.GetAsync();
            _currencySymbol = string.IsNullOrWhiteSpace(appSettings.CurrencySymbol)
                ? (appSettings.Currency == "INR" ? "₹" : "$")
                : appSettings.CurrencySymbol;

            var companyId = _company.CurrentCompany?.LocalId ?? Guid.Empty;
            var items = companyId == Guid.Empty ? [] : await _svc.GetAllAsync(companyId);
            LblCount.Text = $"{items.Count} payment{(items.Count == 1 ? string.Empty : "s")}";
            List.Children.Clear();
            foreach (var p in items.OrderByDescending(i => i.PaymentDate))
                List.Children.Add(BuildRow(p));
            if (items.Count == 0)
            {
                List.Children.Add(new Label
                {
                    Text = "No payments found.",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#64748B"),
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 40)
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Payments] Load failed: {ex}");
            List.Children.Clear();
            List.Children.Add(new Label
            {
                Text = "Payments could not be loaded. Pull down to retry.",
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

    private async void OnRefreshing(object? s, EventArgs e)
    {
        try { await LoadForHostAsync(); }
        finally { Refresher.IsRefreshing = false; }
    }

    private View BuildRow(PaymentModel p)
    {
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
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 12
        };

        var left = new VerticalStackLayout { Spacing = 4 };
        left.Children.Add(new Label { Text = p.PaymentNumber, FontSize = 14, FontAttributes = FontAttributes.Bold });
        left.Children.Add(new Label { Text = p.PartyName, FontSize = 13, TextColor = Color.FromArgb("#64748B") });
        left.Children.Add(new Label
        {
            Text = $"{p.PaymentDate:dd MMM yyyy}  •  {p.MethodLabel}",
            FontSize = 11,
            TextColor = Color.FromArgb("#94A3B8")
        });

        var right = new VerticalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label
        {
            Text = $"{_currencySymbol}{p.Amount:N2}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#059669"),
            HorizontalOptions = LayoutOptions.End
        });
        var viewBtn = new Button
        {
            Text = "View",
            FontSize = 12,
            HeightRequest = 36,
            Padding = new Thickness(12, 0),
            CornerRadius = 8,
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#2563EB"),
            BorderColor = Color.FromArgb("#2563EB"),
            BorderWidth = 1
        };
        viewBtn.Clicked += async (_, _) =>
        {
            try
            {
                var page = ActivatorUtilities.CreateInstance<PaymentViewPage>(_services);
                page.PaymentId = p.LocalId.ToString("D");
                await _navigator.NavigateAsync(page, "Payment Details", "Home / Payments / Details");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Payments] View failed: {ex}");
                await DisplayAlert("Payments", "Payment details could not be opened.", "OK");
            }
        };
        right.Children.Add(viewBtn);

        grid.Add(left, 0, 0);
        grid.Add(right, 1, 0);
        border.Content = grid;
        return border;
    }

    private async void OnAddClicked(object? s, EventArgs e)
    {
        try
        {
            var page = ActivatorUtilities.CreateInstance<PaymentFormPage>(_services);
            await _navigator.NavigateAsync(page, "Add Payment", "Home / Payments / Add");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Payments] Add failed: {ex}");
            await DisplayAlert("Payments", "Payment form could not be opened.", "OK");
        }
    }
}
