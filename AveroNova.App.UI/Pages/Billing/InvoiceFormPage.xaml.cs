using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Billing;

public partial class InvoiceFormPage : ContentPage
{
    private readonly IBillingService _billing;
    private readonly ICustomerService _customers;
    private readonly IProductService _products;
    private readonly ICompanyService _company;
    private readonly List<CustomerModel> _customerList = [];
    private readonly List<ProductModel> _productList = [];
    private readonly List<InvoiceLineItem> _lineItems = [];
    private InvoiceModel? _editing;
    public Action? CloseRequested { get; set; }

    public InvoiceFormPage(IBillingService billing, ICustomerService customers, IProductService products, ICompanyService company)
    {
        InitializeComponent();
        _billing = billing; _customers = customers; _products = products; _company = company;
        DateInvoice.Date = DateTime.Today;
        DateDue.Date = DateTime.Today.AddDays(30);
    }

    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    public Task LoadForNewAsync() => LoadAsync();
    public Task LoadForEditAsync(Guid id) => LoadAsync(id);

    private async Task LoadAsync(Guid? id = null)
    {
        try
        {
            ErrorBanner.IsVisible = false;
            var company = await _company.GetCurrentAsync();
            var companyId = company?.LocalId ?? Guid.Empty;
            if (companyId == Guid.Empty) { ShowError("Company is required."); return; }
            _customerList.Clear();
            _customerList.AddRange(await _customers.GetAllAsync(companyId));
            CustomerPicker.ItemsSource = _customerList.Select(x => x.Name).ToList();
            _productList.Clear();
            _productList.AddRange((await _products.GetAllAsync(companyId)).Where(x => x.Status == ProductStatus.Active).ToList());
            ProductPicker.ItemsSource = _productList.Select(x => $"{x.Name}  •  {x.SKU}  •  Stock: {x.Stock}").ToList();
            if (id.HasValue && id.Value != Guid.Empty)
            {
                _editing = await _billing.GetByIdAsync(id.Value);
                if (_editing == null) { ShowError("Invoice not found."); return; }
                LblTitle.Text = "Edit Sale";
                LblInvoiceNumber.Text = _editing.InvoiceNumber;
                DateInvoice.Date = _editing.InvoiceDate; DateDue.Date = _editing.DueDate;
                CustomerPicker.SelectedIndex = _customerList.FindIndex(x => x.LocalId == _editing.CustomerId);
                EntryDiscount.Text = _editing.DiscountPct.ToString("0.##"); EntryTax.Text = _editing.TaxPct.ToString("0.##");
                EditorNotes.Text = _editing.Notes; _lineItems.Clear(); _lineItems.AddRange(_editing.Items);
            }
            else
            {
                _editing = null; LblTitle.Text = "New Sale";
                LblInvoiceNumber.Text = await _billing.GetNextInvoiceNumberAsync(companyId);
                DateInvoice.Date = DateTime.Today; DateDue.Date = DateTime.Today.AddDays(30);
                CustomerPicker.SelectedIndex = -1; EntryDiscount.Text = "0"; EntryTax.Text = "0"; EditorNotes.Text = string.Empty;
                _lineItems.Clear();
            }
            ProductPicker.SelectedIndex = -1; EntryQuantity.Text = "1"; LblSelectedProduct.IsVisible = false;
            RenderLineItems(); UpdateTotals();
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AveroNova] Invoice form load failed: {ex}"); ShowError("Unable to load sale form."); }
    }

    private void OnProductChanged(object? sender, EventArgs e)
    {
        if (ProductPicker.SelectedIndex < 0 || ProductPicker.SelectedIndex >= _productList.Count) return;
        var product = _productList[ProductPicker.SelectedIndex];
        EntryQuantity.Text = "1";
        LblSelectedProduct.Text = $"{product.Name} • {product.SKU} • Selling price: ₹{product.SellingPrice:N2} • Available: {product.Stock}";
        LblSelectedProduct.IsVisible = true;
    }

    private void OnAddLineItem(object? sender, EventArgs e)
    {
        if (ProductPicker.SelectedIndex < 0 || ProductPicker.SelectedIndex >= _productList.Count) { ShowError("Select a product first."); return; }
        var product = _productList[ProductPicker.SelectedIndex];
        if (!int.TryParse(EntryQuantity.Text, out var quantity) || quantity <= 0) { ShowError("Enter a valid quantity."); return; }
        var existing = _lineItems.FirstOrDefault(x => x.ProductId == product.LocalId);
        var newQuantity = (existing?.Quantity ?? 0) + quantity;
        if (newQuantity > product.Stock) { ShowError($"Insufficient stock for {product.Name}. Available: {product.Stock}."); return; }
        if (existing != null) { existing.Quantity = newQuantity; existing.UnitPrice = product.SellingPrice; existing.TaxPct = product.TaxPercent; }
        else _lineItems.Add(new InvoiceLineItem { ProductId = product.LocalId, ProductName = product.Name, SKU = product.SKU, UnitPrice = product.SellingPrice, Quantity = quantity, TaxPct = product.TaxPercent, DiscountPct = product.DiscountPercent });
        ProductPicker.SelectedIndex = -1; EntryQuantity.Text = "1"; LblSelectedProduct.IsVisible = false; ErrorBanner.IsVisible = false;
        RenderLineItems(); UpdateTotals();
    }

    private void RenderLineItems()
    {
        LineItemsContainer.Children.Clear(); LblNoItems.IsVisible = _lineItems.Count == 0;
        foreach (var item in _lineItems)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 8, Padding = new Thickness(0, 4) };
            var name = new VerticalStackLayout { Spacing = 2 };
            name.Children.Add(new Label { Text = item.ProductName, FontSize = 13, FontAttributes = FontAttributes.Bold });
            name.Children.Add(new Label { Text = item.SKU, FontSize = 10, TextColor = Color.FromArgb("#64748B") }); row.Add(name, 0, 0);
            row.Add(new Label { Text = item.Quantity.ToString(), FontSize = 13, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center }, 1, 0);
            row.Add(new Label { Text = $"₹{item.UnitPrice:N2}", FontSize = 13, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center }, 2, 0);
            var totalCell = new HorizontalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.End };
            totalCell.Children.Add(new Label { Text = $"₹{item.GrandTotal:N2}", FontSize = 13, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center });
            var remove = new Button { Text = "×", FontSize = 16, WidthRequest = 32, HeightRequest = 32, Padding = 0, BackgroundColor = Colors.Transparent, TextColor = Color.FromArgb("#DC2626") };
            var captured = item; remove.Clicked += (_, _) => { _lineItems.Remove(captured); RenderLineItems(); UpdateTotals(); }; totalCell.Children.Add(remove); row.Add(totalCell, 3, 0);
            LineItemsContainer.Children.Add(row);
        }
    }

    private void UpdateTotals()
    {
        var subtotal = _lineItems.Sum(x => x.LineTotal);
        var discountPct = decimal.TryParse(EntryDiscount?.Text, out var d) ? Math.Max(0, d) : 0;
        var taxPct = decimal.TryParse(EntryTax?.Text, out var t) ? Math.Max(0, t) : 0;
        var discount = subtotal * discountPct / 100; var tax = (subtotal - discount) * taxPct / 100; var total = subtotal - discount + tax;
        LblSubtotal.Text = $"₹{subtotal:N2}"; LblDiscount.Text = $"-₹{discount:N2}"; LblTax.Text = $"+₹{tax:N2}"; LblGrandTotal.Text = $"₹{total:N2}";
    }

    private void OnTotalsChanged(object? sender, TextChangedEventArgs e) => UpdateTotals();
    private async void OnSaveClicked(object? sender, EventArgs e) => await SaveAsync(InvoiceStatus.Sent);
    private async void OnSaveDraftClicked(object? sender, EventArgs e) => await SaveAsync(InvoiceStatus.Draft);

    private async Task SaveAsync(InvoiceStatus status)
    {
        ErrorBanner.IsVisible = false;
        if (CustomerPicker.SelectedIndex < 0) { ShowError("Select a customer."); return; }
        if (_lineItems.Count == 0) { ShowError("Add at least one product."); return; }
        if (_lineItems.Any(x => x.ProductId == Guid.Empty || x.Quantity <= 0 || x.UnitPrice < 0)) { ShowError("Check product, quantity and price for every line."); return; }
        var company = await _company.GetCurrentAsync(); var companyId = company?.LocalId ?? Guid.Empty;
        if (companyId == Guid.Empty) { ShowError("Company is required."); return; }
        var customer = _customerList[CustomerPicker.SelectedIndex]; var invoice = _editing ?? new InvoiceModel { CompanyId = companyId };
        invoice.CompanyId = companyId; invoice.InvoiceNumber = LblInvoiceNumber.Text?.Trim() ?? string.Empty; invoice.CustomerId = customer.LocalId; invoice.CustomerName = customer.Name;
        invoice.InvoiceDate = DateInvoice.Date ?? DateTime.Today; invoice.DueDate = DateDue.Date ?? DateTime.Today.AddDays(30); invoice.Items = [.. _lineItems];
        invoice.DiscountPct = decimal.TryParse(EntryDiscount.Text, out var discountPct) ? Math.Max(0, discountPct) : 0; invoice.TaxPct = decimal.TryParse(EntryTax.Text, out var taxPct) ? Math.Max(0, taxPct) : 0;
        invoice.Notes = EditorNotes.Text?.Trim() ?? string.Empty; invoice.Status = status;
        var result = _editing == null ? await _billing.CreateAsync(invoice) : await _billing.UpdateAsync(invoice);
        if (result.Ok) await CloseAsync(); else ShowError(result.Error ?? "Unable to save sale.");
    }

    private async Task CloseAsync()
    {
        if (CloseRequested != null) { CloseRequested.Invoke(); return; }
        await Shell.Current.GoToAsync("..");
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await CloseAsync();
    private void ShowError(string message) { LblError.Text = message; ErrorBanner.IsVisible = true; }
}
