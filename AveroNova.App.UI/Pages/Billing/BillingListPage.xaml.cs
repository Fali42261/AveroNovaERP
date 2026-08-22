using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Billing;

public partial class BillingListPage : ContentPage
{
    private readonly IBillingService _svc;
    private readonly ICompanyService _company;
    private List<InvoiceModel> _all = [];
    private string _filter = "All";

    public BillingListPage(IBillingService svc, ICompanyService company)
    { InitializeComponent(); _svc = svc; _company = company; BuildFilterTabs(); }

    public Task ReloadAsync() => LoadAsync();
    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    private async void OnRefreshing(object s, EventArgs e) { await LoadAsync(); Refresher.IsRefreshing = false; }

    private void BuildFilterTabs()
    {
        FilterTabs.Children.Clear();
        var statuses = new[] { "All", "Draft", "Sent", "Partial", "Paid", "Overdue", "Cancelled" };
        foreach (var st in statuses)
        {
            var btn = new Button
            {
                Text = st,
                FontSize = 12,
                HeightRequest = 34,
                Padding = new Thickness(14, 0),
                CornerRadius = 17,
                BorderWidth = 1,
                BorderColor = Color.FromArgb("#E2E8F0"),
                BackgroundColor = st == _filter ? Color.FromArgb("#2563EB") : Colors.Transparent,
                TextColor = st == _filter ? Colors.White : Color.FromArgb("#64748B")
            };
            var captured = st;
            btn.Clicked += async (_, _) =>
            {
                _filter = captured;
                BuildFilterTabs();
                await LoadAsync();
            };
            FilterTabs.Children.Add(btn);
        }
    }

    private async Task LoadAsync()
    {
        _all = await _svc.GetAllAsync(_company.CurrentCompany?.LocalId ?? Guid.Empty);
        var shown = _filter == "All" ? _all : _all.Where(i => i.StatusLabel == _filter).ToList();
        LblCount.Text = $"{shown.Count} invoice{(shown.Count == 1 ? "" : "s")}";
        InvoiceList.Children.Clear();
        if (shown.Count == 0)
        {
            InvoiceList.Children.Add(new Label { Text = "No invoices found.", FontSize = 14, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 40) });
            return;
        }
        foreach (var inv in shown.OrderByDescending(i => i.InvoiceDate)) InvoiceList.Children.Add(BuildRow(inv));
    }

    private View BuildRow(InvoiceModel inv)
    {
        var (statusBg, statusColor) = inv.Status switch
        {
            InvoiceStatus.Paid => ("#ECFDF5", "#059669"),
            InvoiceStatus.Overdue => ("#FEF2F2", "#DC2626"),
            InvoiceStatus.Sent => ("#EFF6FF", "#2563EB"),
            InvoiceStatus.Draft => ("#F9FAFB", "#6B7280"),
            InvoiceStatus.PartialPaid => ("#FFFBEB", "#D97706"),
            _ => ("#F3F4F6", "#9CA3AF")
        };

        var border = new Border { BackgroundColor = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E293B") : Colors.White, Stroke = Color.FromArgb("#E2E8F0"), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) }, Padding = new Thickness(14, 12) };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12 };
        var left = new VerticalStackLayout { Spacing = 4 };
        left.Children.Add(new Label { Text = inv.InvoiceNumber, FontSize = 14, FontAttributes = FontAttributes.Bold });
        left.Children.Add(new Label { Text = inv.CustomerName, FontSize = 13, TextColor = Color.FromArgb("#64748B") });
        left.Children.Add(new Label { Text = inv.InvoiceDate.ToString("dd MMM yyyy") + $"  •  Due: {inv.DueDate:dd MMM yyyy}", FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });

        var right = new VerticalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label { Text = $"${inv.GrandTotal:N2}", FontSize = 15, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.End });
        var badge = new Border { BackgroundColor = Color.FromArgb(statusBg), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) }, Padding = new Thickness(8, 3) };
        badge.Content = new Label { Text = inv.StatusLabel, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(statusColor) };
        right.Children.Add(badge);
        if (inv.DueAmount > 0) right.Children.Add(new Label { Text = $"Due: ${inv.DueAmount:N2}", FontSize = 11, TextColor = Color.FromArgb("#D97706"), HorizontalOptions = LayoutOptions.End });

        var viewBtn = new Button { Text = "View", Style = TryStyle("SmallSecondaryButton") };
        viewBtn.Clicked += async (_, _) => await Shell.Current.GoToAsync($"{AppRoutes.InvoiceView}?id={inv.LocalId}");
        right.Children.Add(viewBtn);
        grid.Add(left, 0, 0);
        grid.Add(right, 1, 0);
        border.Content = grid;
        return border;
    }

    private async void OnNewClicked(object s, EventArgs e) => await Shell.Current.GoToAsync(AppRoutes.InvoiceNew);

    private static Style? TryStyle(string key)
        => Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Style style ? style : null;
}
