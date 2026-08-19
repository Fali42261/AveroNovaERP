using AveroNova.Domain.Constants;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.SubscriptionAccess;

public static class SubscriptionRestrictionView
{
    public static View Create(string? message = null)
    {
        var card = new Border
        {
            Padding = 24,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(16) },
            BackgroundColor = Color.FromArgb("#FEF2F2"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Start
        };

        card.Content = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = message ?? SubscriptionMessages.FreeTrialExpiredAccess,
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#991B1B"),
                    LineBreakMode = LineBreakMode.WordWrap
                },
                new Label
                {
                    Text = "Subscription-controlled features are unavailable for the current company.",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#7F1D1D"),
                    LineBreakMode = LineBreakMode.WordWrap
                }
            }
        };

        return card;
    }
}
