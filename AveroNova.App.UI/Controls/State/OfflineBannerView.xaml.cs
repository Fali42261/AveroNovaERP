namespace AveroNova.App.UI.Controls.State;

public partial class OfflineBannerView : ContentView
{
    public static readonly BindableProperty PendingCountProperty =
        BindableProperty.Create(nameof(PendingCount), typeof(int), typeof(OfflineBannerView), 0,
            propertyChanged: (b, _, n) =>
            {
                var v = (OfflineBannerView)b;
                int count = (int)n;
                v.LblDetail.Text = count > 0
                    ? $"{count} change{(count == 1 ? "" : "s")} waiting to sync."
                    : "Changes will be saved locally and synchronized when you are online.";
            });

    public int PendingCount
    {
        get => (int)GetValue(PendingCountProperty);
        set => SetValue(PendingCountProperty, value);
    }

    public OfflineBannerView() => InitializeComponent();
}
