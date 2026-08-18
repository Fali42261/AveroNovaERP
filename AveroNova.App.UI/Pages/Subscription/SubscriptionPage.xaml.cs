using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Subscription;

public partial class SubscriptionPage : ContentPage
{
    private readonly ISubscriptionService _svc;
    private SubscriptionModel? _subscription;

    public SubscriptionPage(ISubscriptionService svc)
    { InitializeComponent(); _svc = svc; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async void OnRefreshing(object s, EventArgs e)
    {
        await LoadAsync();
        Refresher.IsRefreshing = false;
    }

    private async Task LoadAsync()
    {
        _subscription = await _svc.GetCurrentAsync();
        BuildContent();
    }

    private void BuildContent()
    {
        SubscriptionContent.Children.Clear();
        var appResources = Microsoft.Maui.Controls.Application.Current!.Resources;
        var cardStyle = ResolveStyle(appResources, "AppCard");
        var dividerStyle = ResolveStyle(appResources, "Divider");
        var primaryButton = ResolveStyle(appResources, "PrimaryButton");
        var secondaryButton = ResolveStyle(appResources, "SecondaryButton");

        var currentCard = new Border { Style = cardStyle };
        var cVsl = new VerticalStackLayout { Spacing = 12 };
        cVsl.Children.Add(new Label { Text = "Current Plan", FontSize = 14, FontAttributes = FontAttributes.Bold });
        cVsl.Children.Add(new BoxView { Style = dividerStyle });
        var planRow = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), RowDefinitions = new RowDefinitionCollection(new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)) };
        planRow.Add(new Label { Text = _subscription?.PlanName ?? "Free Trial", FontSize = 18, FontAttributes = FontAttributes.Bold }, 0, 0);
        var statusBadge = new Border
        {
            BackgroundColor = (_subscription?.IsActive ?? true) ? Color.FromArgb("#ECFDF5") : Color.FromArgb("#FEF2F2"),
            StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
            Padding = new Thickness(10, 4), VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End
        };
        var statusText = _subscription?.IsTrial == true
            ? "Trial"
            : ((_subscription?.IsActive ?? true) ? "Active" : "Inactive");
        statusBadge.Content = new Label { Text = statusText, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = (_subscription?.IsActive ?? true) ? Color.FromArgb("#059669") : Color.FromArgb("#DC2626") };
        planRow.Add(statusBadge, 1, 0);
        var priceText = _subscription?.IsTrial == true
            ? "15-day free trial"
            : $"${_subscription?.Price ?? 0:N2}/{_subscription?.BillingLabel ?? "month"}";
        planRow.Add(new Label { Text = priceText, FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#2563EB"), Margin = new Thickness(0, 4, 0, 0) }, 0, 1);
        cVsl.Children.Add(planRow);
        var renewLabel = new Label
        {
            Text = _subscription?.IsTrial == true
                ? $"Trial ends: {_subscription.ExpiryDate:dd MMM yyyy}"
                : $"Renews: {_subscription?.ExpiryDate:dd MMM yyyy}",
            FontSize = 12,
            TextColor = Color.FromArgb("#64748B"),
            Margin = new Thickness(0, 8, 0, 0)
        };
        cVsl.Children.Add(renewLabel);
        currentCard.Content = cVsl;

        var limitsCard = new Border { Style = cardStyle };
        var lVsl = new VerticalStackLayout { Spacing = 12 };
        lVsl.Children.Add(new Label { Text = "Plan Limits", FontSize = 14, FontAttributes = FontAttributes.Bold });
        lVsl.Children.Add(new BoxView { Style = dividerStyle });
        void AddLimit(string label, string value)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), Margin = new Thickness(0, 2) };
            g.Add(new Label { Text = label, FontSize = 13, TextColor = Color.FromArgb("#64748B") }, 0, 0);
            g.Add(new Label { Text = value, FontSize = 13, FontAttributes = FontAttributes.Bold }, 1, 0);
            lVsl.Children.Add(g);
        }
        AddLimit("Users", $"{_subscription?.MaxUsers ?? 1}");
        AddLimit("Companies", $"{_subscription?.MaxCompanies ?? 1}");
        AddLimit("Storage", $"{_subscription?.MaxStorageMB ?? 500} MB");
        limitsCard.Content = lVsl;

        var actionsCard = new Border { Style = cardStyle };
        var aVsl = new VerticalStackLayout { Spacing = 12 };
        aVsl.Children.Add(new Label { Text = "Manage Plan", FontSize = 14, FontAttributes = FontAttributes.Bold });
        aVsl.Children.Add(new BoxView { Style = dividerStyle });
        var upgradeBtn = new Button { Text = "Upgrade Plan", Style = primaryButton, HeightRequest = 40, FontSize = 13 };
        upgradeBtn.Clicked += async (_, _) => await DisplayAlert("Upgrade", "Upgrade options coming soon.", "OK");
        aVsl.Children.Add(upgradeBtn);
        var manageBtn = new Button { Text = "Billing Details", Style = secondaryButton, HeightRequest = 40, FontSize = 13 };
        manageBtn.Clicked += async (_, _) => await DisplayAlert("Billing", "Billing management coming soon.", "OK");
        aVsl.Children.Add(manageBtn);
        actionsCard.Content = aVsl;

        SubscriptionContent.Children.Add(currentCard);
        SubscriptionContent.Children.Add(limitsCard);
        SubscriptionContent.Children.Add(actionsCard);
    }

    private static Style? ResolveStyle(ResourceDictionary resources, string key)
        => resources.TryGetValue(key, out var value) && value is Style style ? style : null;
}
