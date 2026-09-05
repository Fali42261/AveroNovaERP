namespace AveroNova.App.UI.Helpers;

public enum AppToastKind { Info, Success, Error }

public static class AppToast
{
    public static async Task ShowAsync(ContentPage page, string message, AppToastKind kind = AppToastKind.Error)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            Grid host;
            if (page.Content is Grid existing && existing.StyleId == "AppToastHost") host = existing;
            else
            {
                var original = page.Content;
                page.Content = null;
                host = new Grid { StyleId = "AppToastHost" };
                if (original is not null) host.Children.Add(original);
                page.Content = host;
            }

            var colors = kind switch
            {
                AppToastKind.Success => ("#ECFDF5", "#047857", "#A7F3D0"),
                AppToastKind.Info => ("#EFF6FF", "#1D4ED8", "#BFDBFE"),
                _ => ("#FEF2F2", "#DC2626", "#FECACA")
            };
            var toast = new Border
            {
                BackgroundColor = Color.FromArgb(colors.Item1), Stroke = Color.FromArgb(colors.Item3),
                StrokeThickness = 1, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(16, 11), Margin = new Thickness(20, 18),
                HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Start,
                MaximumWidthRequest = 560, Opacity = 0, ZIndex = 1000,
                Content = new Label { Text = message, FontSize = 13, TextColor = Color.FromArgb(colors.Item2), LineBreakMode = LineBreakMode.WordWrap }
            };
            host.Children.Add(toast);
            await toast.FadeTo(1, 120);
            await Task.Delay(3200);
            await toast.FadeTo(0, 180);
            host.Children.Remove(toast);
        });
    }
}

