using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Purchases;

[QueryProperty(nameof(EditId), "id")]
public partial class PurchaseFormPage : ContentPage
{
    private readonly IPurchaseService _svc;
    private readonly ICompanyService _company;
    private readonly IProductService _products;
    private readonly List<ProductModel> _productList = [];
    private readonly List<PurchaseLineItem> _lineItems = [];
    private PurchaseModel? _editing;
    public string? EditId { get; set; }
    public Action? CloseRequested { get; set; }

    public PurchaseFormPage(IPurchaseService svc, ICompanyService company, IProductService products)
    { InitializeComponent(); _svc = svc; _company = company; _products = products; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Guid? id = !string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var parsed) ? parsed : null;
        await LoadAsync(id);
    }

    public async Task LoadAsync(Guid? id = null)
    {
        ErrorBanner.IsVisible = false; _editing = null; _lineItems.Clear();
        var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        _productList.Clear();
        if (cid != Guid.Empty) _productList.AddRange((await _products.GetAllAsync(cid)).Where(x => x.Status == ProductStatus.Active));
        ProductPicker.ItemsSource = _productList.Select(x => $"{x.Name} • {x.SKU}").ToList();
        EntrySupplier.Text = string.Empty; EntryRef.Text = string.Empty; EditorNotes.Text = string.Empty; EntryPaid.Text = "0";
        DatePurchase.Date = DateTime.Today; DateDue.Date = DateTime.Today.AddDays(30); PaymentPicker.SelectedIndex = 0; StatusPicker.SelectedIndex = 0;
        ProductPicker.SelectedIndex = -1; EntryQuantity.Text = "1"; EntryUnitPrice.Text = string.Empty; EntryItemTax.Text = "0"; LblSelectedProduct.IsVisible = false;
        if (id.HasValue && id.Value != Guid.Empty)
        {
            _editing = await _svc.GetByIdAsync(id.Value);
            if (_editing == null) { ShowError("Purchase not found."); return; }
            LblTitle.Text = "Edit Purchase"; EntrySupplier.Text = _editing.SupplierName; DatePurchase.Date = _editing.PurchaseDate; DateDue.Date = _editing.DueDate;
            EntryRef.Text = _editing.Reference; EditorNotes.Text = _editing.Notes; EntryPaid.Text = _editing.PaidAmount.ToString("0.00");
            PaymentPicker.SelectedIndex = PaymentIndex(_editing.PaymentMethod); StatusPicker.SelectedIndex = StatusIndex(_editing.Status); _lineItems.AddRange(_editing.Items);
        }
        else LblTitle.Text = "New Purchase";
        RenderLineItems(); UpdateTotals();
    }

    private void OnProductChanged(object? sender, EventArgs e)
    {
        if (ProductPicker.SelectedIndex < 0 || ProductPicker.SelectedIndex >= _productList.Count) return;
        var p = _productList[ProductPicker.SelectedIndex]; EntryQuantity.Text = "1"; EntryUnitPrice.Text = p.PurchasePrice.ToString("0.00"); EntryItemTax.Text = p.TaxPercent.ToString("0.##");
        LblSelectedProduct.Text = $"{p.Name} • {p.SKU} • Current stock: {p.Stock}"; LblSelectedProduct.IsVisible = true;
    }

    private void OnAddLineItem(object? sender, EventArgs e)
    {
        ErrorBanner.IsVisible = false;
        if (ProductPicker.SelectedIndex < 0 || ProductPicker.SelectedIndex >= _productList.Count) { ShowError("Select a product."); return; }
        if (!int.TryParse(EntryQuantity.Text, out var qty) || qty <= 0) { ShowError("Enter a valid quantity."); return; }
        if (!decimal.TryParse(EntryUnitPrice.Text, out var price) || price < 0) { ShowError("Enter a valid unit price."); return; }
        var tax = decimal.TryParse(EntryItemTax.Text, out var parsedTax) ? Math.Max(0, parsedTax) : 0; var p = _productList[ProductPicker.SelectedIndex];
        var existing = _lineItems.FirstOrDefault(x => x.ProductId == p.LocalId);
        if (existing != null) { existing.Quantity += qty; existing.UnitPrice = price; existing.TaxPct = tax; }
        else _lineItems.Add(new PurchaseLineItem { ProductId = p.LocalId, ProductName = p.Name, SKU = p.SKU, Quantity = qty, UnitPrice = price, TaxPct = tax });
        ProductPicker.SelectedIndex = -1; EntryQuantity.Text = "1"; EntryUnitPrice.Text = string.Empty; EntryItemTax.Text = "0"; LblSelectedProduct.IsVisible = false;
        RenderLineItems(); UpdateTotals();
    }

    private void RenderLineItems()
    {
        LineItemsContainer.Children.Clear(); LblNoItems.IsVisible = _lineItems.Count == 0;
        foreach (var item in _lineItems)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 10, Padding = new Thickness(0, 5) };
            var info = new VerticalStackLayout { Spacing = 2 }; info.Children.Add(new Label { Text = item.ProductName, FontSize = 13, FontAttributes = FontAttributes.Bold }); info.Children.Add(new Label { Text = item.SKU, FontSize = 10, TextColor = Color.FromArgb("#64748B") }); row.Add(info, 0, 0);
            row.Add(new Label { Text = $"{item.Quantity} × ₹{item.UnitPrice:N2}", FontSize = 12, VerticalOptions = LayoutOptions.Center }, 1, 0);
            row.Add(new Label { Text = $"₹{item.GrandTotal:N2}", FontSize = 13, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }, 2, 0);
            var remove = new Button { Text = "×", WidthRequest = 32, HeightRequest = 32, Padding = 0, FontSize = 16, BackgroundColor = Colors.Transparent, TextColor = Color.FromArgb("#DC2626") };
            var captured = item; remove.Clicked += (_, _) => { _lineItems.Remove(captured); RenderLineItems(); UpdateTotals(); }; row.Add(remove, 3, 0); LineItemsContainer.Children.Add(row);
        }
    }

    private void OnTotalsChanged(object? sender, TextChangedEventArgs e) => UpdateTotals();
    private void UpdateTotals()
    {
        var subtotal = _lineItems.Sum(x => x.LineTotal); var tax = _lineItems.Sum(x => x.TaxAmount); var total = subtotal + tax;
        var paid = decimal.TryParse(EntryPaid?.Text, out var p) ? Math.Max(0, p) : 0;
        LblSubtotal.Text = $"₹{subtotal:N2}"; LblTax.Text = $"₹{tax:N2}"; LblGrandTotal.Text = $"₹{total:N2}"; LblDue.Text = $"₹{Math.Max(0, total - paid):N2}";
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        ErrorBanner.IsVisible = false;
        if (string.IsNullOrWhiteSpace(EntrySupplier.Text)) { ShowError("Supplier name is required."); return; }
        if (_lineItems.Count == 0) { ShowError("Add at least one product."); return; }
        if (StatusPicker.SelectedIndex < 0) { ShowError("Select purchase status."); return; }
        var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty; if (cid == Guid.Empty) { ShowError("Company is required."); return; }
        var paid = decimal.TryParse(EntryPaid.Text, out var parsedPaid) ? Math.Max(0, parsedPaid) : 0; var total = _lineItems.Sum(x => x.GrandTotal);
        if (paid > total) { ShowError("Paid amount cannot be greater than purchase total."); return; }
        var model = _editing ?? new PurchaseModel { CompanyId = cid, SupplierId = Guid.NewGuid() };
        model.CompanyId = cid; if (model.SupplierId == Guid.Empty) model.SupplierId = Guid.NewGuid(); model.SupplierName = EntrySupplier.Text.Trim();
        model.PurchaseDate = DatePurchase.Date ?? DateTime.Today; model.DueDate = DateDue.Date ?? DateTime.Today.AddDays(30); model.Reference = EntryRef.Text?.Trim() ?? ""; model.Notes = EditorNotes.Text?.Trim() ?? "";
        model.Items = [.. _lineItems]; model.PaidAmount = paid; model.PaymentMethod = PaymentFromIndex(PaymentPicker.SelectedIndex); model.Status = StatusFromIndex(StatusPicker.SelectedIndex);
        if (string.IsNullOrEmpty(model.PurchaseNumber)) model.PurchaseNumber = await _svc.GetNextPurchaseNumberAsync(cid);
        var (ok, err) = _editing == null ? await _svc.CreateAsync(model) : await _svc.UpdateAsync(model);
        if (ok) await CloseAsync(); else ShowError(err ?? "Save failed.");
    }

    private static int PaymentIndex(PaymentMethod method) => method switch { PaymentMethod.Cash => 0, PaymentMethod.BankTransfer => 1, PaymentMethod.CreditCard => 2, PaymentMethod.DebitCard => 3, PaymentMethod.Cheque => 4, PaymentMethod.Online => 5, _ => 0 };
    private static PaymentMethod PaymentFromIndex(int i) => i switch { 1 => PaymentMethod.BankTransfer, 2 => PaymentMethod.CreditCard, 3 => PaymentMethod.DebitCard, 4 => PaymentMethod.Cheque, 5 => PaymentMethod.Online, _ => PaymentMethod.Cash };
    private static int StatusIndex(PurchaseStatus status) => status switch { PurchaseStatus.Ordered => 1, PurchaseStatus.Received => 2, PurchaseStatus.Cancelled => 3, _ => 0 };
    private static PurchaseStatus StatusFromIndex(int i) => i switch { 1 => PurchaseStatus.Ordered, 2 => PurchaseStatus.Received, 3 => PurchaseStatus.Cancelled, _ => PurchaseStatus.Draft };

    private async Task CloseAsync() { if (CloseRequested != null) { CloseRequested.Invoke(); return; } await Shell.Current.GoToAsync(".."); }
    private async void OnBackClicked(object s, EventArgs e) => await CloseAsync();
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
