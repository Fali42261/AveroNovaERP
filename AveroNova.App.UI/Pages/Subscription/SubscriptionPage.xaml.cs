using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Domain.Constants;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Subscription;

public partial class SubscriptionPage : ContentPage
{
    private readonly ISubscriptionService _svc;
    private SubscriptionModel? _subscription;

    public SubscriptionPage(ISubscriptionService svc)
    {
        InitializeComponent();
        _svc = svc;
    }

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

        if (_subscription?.IsExpired == true)
        {
            SubscriptionContent.Children.Add(SubscriptionRestrictionView.Create(SubscriptionMessages.FreeTrialExpiredAccess));
        }

        var currentCard = new Border { Style = cardStyle };
        var cVsl = new VerticalStackLayout { Spacing = 12 };
        cVsl.Children.Add(new Label { Text = "Current Plan", FontSize = 14, FontAttributes = FontAttributes.Bold });
        cVsl.Children.Add(new BoxView { Style = dividerStyle });
        var planRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            RowDefinitions = new RowDefinitionCollection(
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto))
        };
        planRow.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(_subscription?.PlanName) ? "Free Trial" : _subscription.PlanName,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        }, 0, 0);

        var active = _subscription?.IsActive == true;
        var statusBadge = new Border
        {
            BackgroundColor = active ? Color.FromArgb("#ECFDF5") : Color.FromArgb("#FEF2F2"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
            Padding = new Thickness(10, 4),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };
        var statusText = _subscription?.IsExpired == true
            ? "Expired"
            : _subscription?.IsTrial == true ? "Trial" : "Active";
        statusBadge.Content = new Label
        {
            Text = statusText,
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = active ? Color.FromArgb("#059669") : Color.FromArgb("#DC2626")
        };
        planRow.Add(statusBadge, 1, 0);
        planRow.Add(new Label
        {
            Text = "15-day free trial",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2563EB"),
            Margin = new Thickness(0, 4, 0, 0)
        }, 0, 1);
        cVsl.Children.Add(planRow);
        cVsl.Children.Add(new Label
        {
            Text = _subscription == null
                ? "No subscription found for the current company."
                : $"Valid from {_subscription.StartDate:dd MMM yyyy} to {_subscription.ExpiryDate:dd MMM yyyy}",
            FontSize = 12,
            TextColor = Color.FromArgb("#64748B"),
            Margin = new Thickness(0, 8, 0, 0)
        });
        currentCard.Content = cVsl;
        SubscriptionContent.Children.Add(currentCard);
    }

    private static Style? ResolveStyle(ResourceDictionary resources, string key)
        => resources.TryGetValue(key, out var value) && value is Style style ? style : null;
}
