using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Controls.Status;

public partial class ConnectionStatusView : ContentView
{
    public static readonly BindableProperty StatusProperty =
        BindableProperty.Create(nameof(Status), typeof(ConnectivityStatus), typeof(ConnectionStatusView),
            ConnectivityStatus.Online, propertyChanged: (b, _, n) => ((ConnectionStatusView)b).ApplyStatus((ConnectivityStatus)n));

    public ConnectivityStatus Status
    {
        get => (ConnectivityStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public ConnectionStatusView() => InitializeComponent();

    private void ApplyStatus(ConnectivityStatus status)
    {
        var (dot, label, bg, border, text) = status switch
        {
            ConnectivityStatus.Online      => ("#10B981", "Online",      "#ECFDF5", "#A7F3D0", "#059669"),
            ConnectivityStatus.Offline     => ("#EF4444", "Offline",     "#FEF2F2", "#FECACA", "#DC2626"),
            ConnectivityStatus.Syncing     => ("#3B82F6", "Syncing",     "#EFF6FF", "#BFDBFE", "#2563EB"),
            ConnectivityStatus.Synced      => ("#10B981", "Synced",      "#ECFDF5", "#A7F3D0", "#059669"),
            ConnectivityStatus.SyncFailed  => ("#EF4444", "Sync Failed", "#FEF2F2", "#FECACA", "#DC2626"),
            ConnectivityStatus.PendingSync => ("#F59E0B", "Pending",     "#FFFBEB", "#FDE68A", "#D97706"),
            _                              => ("#9CA3AF", "Unknown",     "#F3F4F6", "#E5E7EB", "#6B7280")
        };

        Dot.BackgroundColor        = Color.FromArgb(dot);
        LblStatus.Text             = label;
        LblStatus.TextColor        = Color.FromArgb(text);
        Container.BackgroundColor  = Color.FromArgb(bg);
        Container.Stroke           = Color.FromArgb(border);
    }
}
