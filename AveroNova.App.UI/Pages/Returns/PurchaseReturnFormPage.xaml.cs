using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Returns;

public partial class PurchaseReturnFormPage : ContentPage, IHostedPage
{
    private readonly IReturnService _service;
    private readonly ICompanyService _company;
    private readonly IPurchaseService _purchases;
    private readonly IMainContentNavigator _navigator;
    private readonly Dictionary<Guid, Entry> _quantityEntries = [];
    private List<PurchaseModel> _purchaseItems = [];
    private PurchaseReturnModel? _editing;

    public Guid? EditId { get; set; }

    public PurchaseReturnFormPage(IReturnService service, ICompanyService company,
        IPurchaseService purchases, IMainContentNavigator navigator)
    {
        InitializeComponent();
        _service = service;
        _company = company;
        _purchases = purchases;
        _navigator = navigator;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadForHostAsync();
    }

    public async Task LoadForHostAsync()
    {
        DateReturn.Date = DateTime.Today;
        StatusPicker.SelectedIndex = 0;
        if (EditId.HasValue && _editing is null)
            _editing = await _service.GetPurchaseReturnByIdAsync(EditId.Value);

        var companyId = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        _purchaseItems = (await _purchases.GetAllAsync(companyId))
            .Where(x => x.Status == PurchaseStatus.Received && (_editing is null || x.LocalId == _editing.PurchaseId))
            .ToList();
        PurchasePicker.ItemsSource = _purchaseItems.Select(x => $"{x.PurchaseNumber} — {x.SupplierName}").ToList();

        if (_editing is null) return;
        PurchasePicker.SelectedIndex = _purchaseItems.FindIndex(x => x.LocalId == _editing.PurchaseId);
        DateReturn.Date = _editing.ReturnDate;
        EntryRefund.Text = _editing.RefundAmount.ToString("0.##");
        ReasonPicker.SelectedItem = _editing.Reason;
        StatusPicker.SelectedIndex = (int)_editing.Status;
        EditorNotes.Text = _editing.Notes;
    }

    private void OnPurchaseChanged(object sender, EventArgs e)
    {
        ItemsContainer.Children.Clear();
        _quantityEntries.Clear();
        if (PurchasePicker.SelectedIndex < 0 || PurchasePicker.SelectedIndex >= _purchaseItems.Count) return;

        var purchase = _purchaseItems[PurchasePicker.SelectedIndex];
        foreach (var item in purchase.Items.GroupBy(x => x.ProductId).Select(group => new
                 {
                     ProductId = group.Key,
                     Name = group.First().ProductName,
                     Quantity = group.Sum(x => x.Quantity),
                     UnitPrice = group.First().UnitPrice
                 }))
        {
            var saved = _editing?.Items.Where(x => x.ProductId == item.ProductId).Sum(x => x.Quantity) ?? 0;
            var entry = new Entry
            {
                Text = saved == 0 ? string.Empty : saved.ToString(), Placeholder = "0",
                Keyboard = Keyboard.Numeric, HorizontalTextAlignment = TextAlignment.Center,
                WidthRequest = 90
            };
            _quantityEntries[item.ProductId] = entry;
            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
            grid.Add(new Label { Text = $"{item.Name} · available {item.Quantity} · ${item.UnitPrice:N2}", VerticalOptions = LayoutOptions.Center });
            grid.Add(entry, 1);
            ItemsContainer.Children.Add(new Border { Content = grid, Padding = new Thickness(12, 6) });
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (PurchasePicker.SelectedIndex < 0) { ShowError("Select a received purchase order."); return; }
        if (!decimal.TryParse(EntryRefund.Text, out var refund) || refund <= 0) { ShowError("Refund amount must be greater than zero."); return; }
        if (ReasonPicker.SelectedIndex < 0) { ShowError("Select a return reason."); return; }

        var purchase = _purchaseItems[PurchasePicker.SelectedIndex];
        var sourceItems = purchase.Items.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.First());
        var returnItems = new List<ReturnLineItem>();
        foreach (var (productId, entry) in _quantityEntries)
        {
            if (string.IsNullOrWhiteSpace(entry.Text)) continue;
            if (!int.TryParse(entry.Text, out var quantity) || quantity < 0) { ShowError("Return quantities must be whole numbers."); return; }
            if (quantity == 0) continue;
            var source = sourceItems[productId];
            returnItems.Add(new ReturnLineItem { ProductId=productId,ProductName=source.ProductName,Quantity=quantity,UnitPrice=source.UnitPrice });
        }
        if (returnItems.Count == 0) { ShowError("Enter a quantity for at least one item."); return; }

        var model = _editing ?? new PurchaseReturnModel();
        model.PurchaseId = purchase.LocalId;
        model.PurchaseNumber = purchase.PurchaseNumber;
        model.SupplierId = purchase.SupplierId;
        model.SupplierName = purchase.SupplierName;
        model.ReturnDate = DateReturn.Date ?? DateTime.Today;
        model.Items = returnItems;
        model.RefundAmount = refund;
        model.Reason = ReasonPicker.SelectedItem?.ToString() ?? string.Empty;
        model.Notes = EditorNotes.Text?.Trim() ?? string.Empty;
        model.Status = (ReturnStatus)Math.Max(0, StatusPicker.SelectedIndex);
        model.CompanyId = _company.CurrentCompany?.LocalId ?? Guid.Empty;

        var result = _editing is null
            ? await _service.CreatePurchaseReturnAsync(model)
            : await _service.UpdatePurchaseReturnAsync(model);
        if (result.Ok) await _navigator.GoBackAsync();
        else ShowError(result.Error ?? "Save failed.");
    }

    private async void OnBackClicked(object sender, EventArgs e) => await _navigator.GoBackAsync();
    private void ShowError(string message) { LblError.Text = message; ErrorBanner.IsVisible = true; }
}
