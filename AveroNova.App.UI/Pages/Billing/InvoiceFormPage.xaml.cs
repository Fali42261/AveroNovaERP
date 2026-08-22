using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Billing;

[QueryProperty(nameof(EditId), "id")]
public partial class InvoiceFormPage : ContentPage
{
    private readonly IBillingService _billing;
    private readonly ICustomerService _customers;
    private readonly IProductService _products;
    private readonly ICompanyService _company;

    private List<CustomerModel> _customerList = [];
    private List<ProductModel> _productList = [];
    private readonly List<InvoiceLineItem> _lineItems = [];
    private InvoiceModel? _editing;
    private bool _loaded;
    public string? EditId { get; set; }

    public InvoiceFormPage(IBillingService billing, ICustomerService customers, IProductService products, ICompanyService company)
    {
        InitializeComponent();
        _billing = billing;
        _customers = customers;
        _products = products;
        _company = company;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;

        var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        if (cid == Guid.Empty) { ShowError("Select a company before creating a sale."); return; }

        _customerList = await _customers.GetAllAsync(cid);
        _productList = await _products.GetAllAsync(cid);
        CustomerPicker.ItemsSource = _customerList.Select(c => c.Name).ToList();

        if (string.IsNullOrWhiteSpace(EditId))
        {
            LblTitle.Text = "New Sale";
            LblInvoiceNumber.Text = await _billing.GetNextInvoiceNumberAsync(cid);
            DateInvoice.Date = DateTime.Today;
            DateDue.Date = DateTime.Today.AddDays(30);
            return;
        }

        if (Guid.TryParse(EditId, out var id))
        {
            _editing = await _billing.GetByIdAsync(id);
            if (_editing != null) PopulateForm(_editing);
            else ShowError("Sale not found.");
        }
    }

    private void PopulateForm(InvoiceModel inv)
    {
        LblTitle.Text = "Edit Sale";
        LblInvoiceNumber.Text = inv.InvoiceNumber;
        DateInvoice.Date = inv.InvoiceDate;
        DateDue.Date = inv.DueDate;
        var customerIndex = _customerList.FindIndex(c => c.LocalId == inv.CustomerId);
        if (customerIndex >= 0) CustomerPicker.SelectedIndex = customerIndex;
        EntryDiscount.Text = inv.DiscountPct.ToString("N2");
        EntryTax.Text = inv.TaxPct.ToString("N2");
        EditorNotes.Text = inv.Notes;
        _lineItems.Clear();
        _lineItems.AddRange(inv.Items);
        RebuildLineItems();
    }

    private void OnAddLineItem(object sender, EventArgs e)
    {
        _lineItems.Add(new InvoiceLineItem { Quantity = 1, UnitPrice = 0 });
        RebuildLineItems();
    }

    private void RebuildLineItems()
    {
        LineItemsContainer.Children.Clear();
        for (var i = 0; i < _lineItems.Count; i++)
        {
            var item = _lineItems[i];
            var index = i;
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection(
                    new ColumnDefinition(new GridLength(2, GridUnitType.Star)),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)),
                ColumnSpacing = 8
            };

            var productPicker = new Picker { Title = "Product", BackgroundColor = Colors.Transparent };
            productPicker.ItemsSource = _productList.Select(p => p.Name).ToList();
            var productIndex = _productList.FindIndex(p => p.LocalId == item.ProductId);
            if (productIndex >= 0) productPicker.SelectedIndex = productIndex;
            productPicker.SelectedIndexChanged += (_, _) =>
            {
                if (productPicker.SelectedIndex < 0) return;
                var product = _productList[productPicker.SelectedIndex];
                item.ProductId = product.LocalId;
                item.ProductName = product.Name;
                item.UnitPrice = product.SellingPrice;
                item.TaxPct = product.TaxPercent;
                RebuildLineItems();
            };

            var qtyEntry = new Entry { Text = item.Quantity.ToString(), Keyboard = Keyboard.Numeric, BackgroundColor = Colors.Transparent };
            qtyEntry.TextChanged += (_, _) => { if (int.TryParse(qtyEntry.Text, out var quantity)) { item.Quantity = quantity; UpdateTotals(); } };

            var priceEntry = new Entry { Text = item.UnitPrice.ToString("N2"), Keyboard = Keyboard.Numeric, BackgroundColor = Colors.Transparent };
            priceEntry.TextChanged += (_, _) => { if (decimal.TryParse(priceEntry.Text, out var price)) { item.UnitPrice = price; UpdateTotals(); } };

            var removeButton = new Button { Text = "✕", BackgroundColor = Colors.Transparent, TextColor = Color.FromArgb("#EF4444"), BorderWidth = 0, Padding = new Thickness(4), FontSize = 16 };
            removeButton.Clicked += (_, _) => { _lineItems.RemoveAt(index); RebuildLineItems(); };

            var inputStyle = Resources["InputContainer"] as Style;
            var productBorder = new Border { HeightRequest = 42, Content = productPicker };
            var qtyBorder = new Border { HeightRequest = 42, Content = qtyEntry };
            var priceBorder = new Border { HeightRequest = 42, Content = priceEntry };
            if (inputStyle != null) { productBorder.Style = inputStyle; qtyBorder.Style = inputStyle; priceBorder.Style = inputStyle; }

            row.Add(productBorder, 0, 0);
            row.Add(qtyBorder, 1, 0);
            row.Add(priceBorder, 2, 0);
            row.Add(removeButton, 3, 0);
            LineItemsContainer.Children.Add(row);
        }
        UpdateTotals();
    }

    private void UpdateTotals()
    {
        var subtotal = _lineItems.Sum(i => i.LineTotal);
        var discount = decimal.TryParse(EntryDiscount.Text, out var discountPct) ? subtotal * discountPct / 100 : 0;
        var tax = decimal.TryParse(EntryTax.Text, out var taxPct) ? (subtotal - discount) * taxPct / 100 : 0;
        var total = subtotal - discount + tax;
        LblSubtotal.Text = $"${subtotal:N2}";
        LblDiscount.Text = $"-${discount:N2}";
        LblTax.Text = $"+${tax:N2}";
        LblGrandTotal.Text = $"${total:N2}";
    }

    private void OnTotalsChanged(object sender, TextChangedEventArgs e) => UpdateTotals();
    private async void OnSaveClicked(object sender, EventArgs e) => await SaveAsync(InvoiceStatus.Sent);
    private async void OnSaveDraftClicked(object sender, EventArgs e) => await SaveAsync(InvoiceStatus.Draft);

    private async Task SaveAsync(InvoiceStatus status)
    {
        ErrorBanner.IsVisible = false;
        if (CustomerPicker.SelectedIndex < 0) { ShowError("Select a customer."); return; }
        if (_lineItems.Count == 0) { ShowError("Add at least one product."); return; }
        if (_lineItems.Any(x => x.ProductId == Guid.Empty || x.Quantity <= 0 || x.UnitPrice < 0)) { ShowError("Check product, quantity and price for every line."); return; }

        var customer = _customerList[CustomerPicker.SelectedIndex];
        var invoice = _editing ?? new InvoiceModel { CompanyId = _company.CurrentCompany?.LocalId ?? Guid.Empty };
        invoice.InvoiceNumber = LblInvoiceNumber.Text?.Trim() ?? string.Empty;
        invoice.CustomerId = customer.LocalId;
        invoice.CustomerName = customer.Name;
        invoice.InvoiceDate = DateInvoice.Date;
        invoice.DueDate = DateDue.Date;
        invoice.Items = [.. _lineItems];
        invoice.DiscountPct = decimal.TryParse(EntryDiscount.Text, out var discountPct) ? discountPct : 0;
        invoice.TaxPct = decimal.TryParse(EntryTax.Text, out var taxPct) ? taxPct : 0;
        invoice.Notes = EditorNotes.Text?.Trim() ?? string.Empty;
        invoice.Status = status;

        var (ok, error) = _editing == null ? await _billing.CreateAsync(invoice) : await _billing.UpdateAsync(invoice);
        if (ok) await Shell.Current.GoToAsync("..");
        else ShowError(error ?? "Unable to save sale.");
    }

    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    private void ShowError(string message) { LblError.Text = message; ErrorBanner.IsVisible = true; }
}
