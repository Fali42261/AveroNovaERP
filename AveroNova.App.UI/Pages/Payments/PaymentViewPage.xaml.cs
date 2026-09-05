using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Payments;

[QueryProperty(nameof(PaymentId), "id")]
public partial class PaymentViewPage : ContentPage
{
    private readonly IPaymentService _svc;
    private PaymentModel? _payment;
    public string? PaymentId { get; set; }

    public PaymentViewPage(IPaymentService svc) { InitializeComponent(); _svc = svc; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(PaymentId) && Guid.TryParse(PaymentId, out var id))
        {
            _payment = await _svc.GetByIdAsync(id);
            if (_payment != null) BuildContent(_payment);
        }
    }

    private void BuildContent(PaymentModel p)
    {
        Content.Children.Clear();
        var card = new Border { Style = (Style)Resources["AppCard"] };
        var vsl = new VerticalStackLayout { Spacing = 12 };
        vsl.Children.Add(new Label { Text = $"${p.Amount:N2}", FontSize = 32, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#059669"), HorizontalOptions = LayoutOptions.Center });
        vsl.Children.Add(new Label { Text = p.PaymentNumber, FontSize = 14, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center });
        vsl.Children.Add(new BoxView { Style = (Style)Resources["Divider"] });

        void Row(string l, string v) { var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(new GridLength(130)), new ColumnDefinition(GridLength.Star)) }; g.Add(new Label { Text = l, FontSize = 13, TextColor = Color.FromArgb("#64748B") }, 0, 0); g.Add(new Label { Text = v, FontSize = 13, FontAttributes = FontAttributes.Bold }, 1, 0); vsl.Children.Add(g); }
        Row("Customer", p.PartyName);
        Row("Invoice", p.InvoiceNumber);
        Row("Date", p.PaymentDate.ToString("dd MMM yyyy"));
        Row("Type", p.MethodLabel);
        Row("Reference", p.Reference);
        Row("Status", p.StatusLabel);
        Row("Sync", p.SyncStatus switch { SyncStatus.Synced => "Synced", SyncStatus.SyncFailed => "Sync failed", _ => "Pending sync" });
        if (!string.IsNullOrEmpty(p.Notes)) Row("Notes", p.Notes);
        card.Content = vsl;
        Content.Children.Add(card);
    }

    private async void OnDeleteClicked(object s, EventArgs e)
    {
        if (_payment == null) return;
        if (!await DialogHelper.ConfirmDeleteAsync("Payment", $"Delete {_payment.PaymentNumber}?")) return;
        var (ok, error) = await _svc.DeleteAsync(_payment.LocalId);
        if (!ok)
        {
            await AppToast.ErrorAsync(error ?? "Unable to delete payment.");
            return;
        }
        await AppToast.SuccessAsync("Payment deleted successfully.");
        await Shell.Current.GoToAsync("..");
    }

    private async void OnBackClicked(object s, EventArgs e) => await Shell.Current.GoToAsync("..");
}
