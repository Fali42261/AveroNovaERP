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

    public InvoiceFormPage(IBillingService billing, ICustomerService customers, IProductService products, ICompanyService company)
    {
        InitializeComponent();
        _billing = billing;
        _customers = customers;
        _products = products;
        _company = company;
        DateInvoice.Date = DateTime.Today;
        DateDue.Date = DateTime.Today.AddDays(30);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    public Task LoadForEditAsync(Guid id) => LoadAsync(id);

    private async Task LoadAsync(Guid? id = null)
    {
        try
        {
            var company = await _company.GetCurrentAsync();
            var companyId = company?.LocalId ?? Guid.Empty;
            if (companyId == Guid.Empty)
            {
                ShowError("Company is required.");
                return;
            }

            _customerList.Clear();
            _customerList.AddRange(await _customers.GetAllAsync(companyId));
            CustomerPicker.ItemsSource = _customerList.Select(x => x.Name).ToList();

            _productList.Clear();
            _productList.AddRange(await _products.GetAllAsync(companyId));

            if (id.HasValue && id.Value != Guid.Empty)
            {
                _editing = await _billing.GetByIdAsync(id.Value);
                if (_editing == null)
                {
                    ShowError("Invoice not found.");
                    return;
                }

                LblTitle.Text = "Edit Invoice";
                LblInvoiceNumber.Text = _editing.InvoiceNumber;
                DateInvoice.Date = _editing.InvoiceDate;
                DateDue.Date = _editing.DueDate;
                CustomerPicker.SelectedIndex = _customerList.FindIndex(x => x.LocalId == _editing.CustomerId);
                EntryDiscount.Text = _editing.DiscountPct.ToString("0.##");
                EntryTax.Text = _editing.TaxPct.ToString("0.##");
                EditorNotes.Text = _editing.Notes;
                _lineItems.Clear();
                _lineItems.AddRange(_editing.Items);
            }
            else
            {
                _editing = null;
                LblTitle.Text = "New Invoice";
                LblInvoiceNumber.Text = "Auto-generated";
                DateInvoice.Date = DateTime.Today;
                DateDue.Date = DateTime.Today.AddDays(30);
                _lineItems.Clear();
            }

            RenderLineItems();
            UpdateTotals();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Invoice form load failed: {ex}");
            ShowError("Unable to load invoice form.");
        }
    }

    private void OnAddLineItem(object? sender, EventArgs e)
    {
        var product = _productList.FirstOrDefault();
        if (product == null)
        {
            ShowError("Add a product before creating a sale.");
            return;
        }

        _lineItems.Add(new InvoiceLineItem
        {
            ProductId = product.LocalId,
            ProductName = product.Name,
            SKU = product.SKU,
            UnitPrice = product.SellingPrice,
            Quantity = 1
        });
        RenderLineItems();
        UpdateTotals();
    }

    private void RenderLineItems()
    {
        LineItemsContainer.Children.Clear();
        foreach (var item in _lineItems)
        {
            LineItemsContainer.Children.Add(new Label
            {
                Text = $"{item.ProductName}  × {item.Quantity}  —  {item.GrandTotal:C}",
                FontSize = 13,
                VerticalOptions = LayoutOptions.Center
            });
        }
    }

    private void UpdateTotals()
    {
        var subtotal = _lineItems.Sum(x => x.LineTotal);
        var discountPct = decimal.TryParse(EntryDiscount?.Text, out var d) ? d : 0;
        var taxPct = decimal.TryParse(EntryTax?.Text, out var t) ? t : 0;
        var discount = subtotal * discountPct / 100;
        var tax = (subtotal - discount) * taxPct / 100;
        var total = subtotal - discount + tax;
        LblSubtotal.Text = $"${subtotal:N2}";
        LblDiscount.Text = $"-${discount:N2}";
        LblTax.Text = $"+${tax:N2}";
        LblGrandTotal.Text = $"${total:N2}";
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

        var company = await _company.GetCurrentAsync();
        var companyId = company?.LocalId ?? Guid.Empty;
        if (companyId == Guid.Empty) { ShowError("Company is required."); return; }

        var customer = _customerList[CustomerPicker.SelectedIndex];
        var invoice = _editing ?? new InvoiceModel { CompanyId = companyId };
        invoice.CompanyId = companyId;
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

        (bool ok, string? error) result = _editing == null
            ? await _billing.CreateAsync(invoice)
            : await _billing.UpdateAsync(invoice);

        if (result.ok)
            await Shell.Current.GoToAsync("..");
        else
            ShowError(result.error ?? "Unable to save sale.");
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");
    private void ShowError(string message) { LblError.Text = message; ErrorBanner.IsVisible = true; }
}
