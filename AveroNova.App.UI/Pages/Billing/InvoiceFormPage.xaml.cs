using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Billing;

// ==================================================
// OFFLINE FLOW
// ==================================================
// ViewModel → IBillingService → Local Database
//           → Pending Sync Queue
//           → SyncService → API → Server Database
//
// TODO: Implement during backend phase.
// ==================================================

[QueryProperty(nameof(EditId), "id")]
public partial class InvoiceFormPage : ContentPage
{
    private readonly IBillingService  _billing;
    private readonly ICustomerService _customers;
    private readonly IProductService  _products;
    private readonly ICompanyService  _company;

    private List<CustomerModel>  _customerList = [];
    private List<ProductModel>   _productList  = [];
    private readonly List<InvoiceLineItem> _lineItems = [];
    private InvoiceModel?        _editing;
    public  string?              EditId { get; set; }

    public InvoiceFormPage(IBillingService billing, ICustomerService customers, IProductService products, ICompanyService company)
    { InitializeComponent(); _billing = billing; _customers = customers; _products = products; _company = company; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        _customerList = await _customers.GetAllAsync(cid);
        _productList  = await _products.GetAllAsync(cid);
        CustomerPicker.ItemsSource = _customerList.Select(c => c.Name).ToList();

        if (string.IsNullOrEmpty(EditId))
        {
            var num = await _billing.GetNextInvoiceNumberAsync(cid);
            LblInvoiceNumber.Text = num;
            DateInvoice.Date      = DateTime.Today;
            DateDue.Date          = DateTime.Today.AddDays(30);
        }
        else if (Guid.TryParse(EditId, out var id))
        {
            _editing = await _billing.GetByIdAsync(id);
            if (_editing != null) PopulateForm(_editing);
        }
    }

    private void PopulateForm(InvoiceModel inv)
    {
        LblTitle.Text         = "Edit Invoice";
        LblInvoiceNumber.Text = inv.InvoiceNumber;
        DateInvoice.Date      = inv.InvoiceDate;
        DateDue.Date          = inv.DueDate;
        var ci = _customerList.FindIndex(c => c.LocalId == inv.CustomerId);
        if (ci >= 0) CustomerPicker.SelectedIndex = ci;
        EntryDiscount.Text    = inv.DiscountPct.ToString("N0");
        EntryTax.Text         = inv.TaxPct.ToString("N0");
        EditorNotes.Text      = inv.Notes;
        _lineItems.AddRange(inv.Items);
        RebuildLineItems();
    }

    private void OnAddLineItem(object s, EventArgs e)
    {
        _lineItems.Add(new InvoiceLineItem { Quantity = 1, UnitPrice = 0 });
        RebuildLineItems();
    }

    private void RebuildLineItems()
    {
        LineItemsContainer.Children.Clear();
        for (int i = 0; i < _lineItems.Count; i++)
        {
            var item = _lineItems[i];
            var idx  = i;
            var row  = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(new GridLength(2, GridUnitType.Star)), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 8 };

            var prodPicker = new Picker { Title = "Product", BackgroundColor = Colors.Transparent };
            prodPicker.ItemsSource = _productList.Select(p => p.Name).ToList();
            var pi = _productList.FindIndex(p =>  p.LocalId == item.ProductId);
            //p.ProductId == item.ProductId ||
            if (pi >= 0) prodPicker.SelectedIndex = pi;
            prodPicker.SelectedIndexChanged += (_, _) =>
            {
                if (prodPicker.SelectedIndex < 0) return;
                var p = _productList[prodPicker.SelectedIndex];
                item.ProductId   = p.LocalId;
                item.ProductName = p.Name;
                item.UnitPrice   = p.SellingPrice;
                item.TaxPct      = p.TaxPercent;
                RebuildLineItems();
            };

            var qtyEntry  = new Entry { Text = item.Quantity.ToString(), Keyboard = Keyboard.Numeric, BackgroundColor = Colors.Transparent };
            var priceEntry = new Entry { Text = item.UnitPrice.ToString("N2"), Keyboard = Keyboard.Numeric, BackgroundColor = Colors.Transparent };
            qtyEntry.TextChanged  += (_, _) => { if (int.TryParse(qtyEntry.Text, out var q)) { item.Quantity = q; UpdateTotals(); } };
            priceEntry.TextChanged += (_, _) => { if (decimal.TryParse(priceEntry.Text, out var p)) { item.UnitPrice = p; UpdateTotals(); } };

            var removeBtn = new Button { Text = "&#x2715;", BackgroundColor = Colors.Transparent, TextColor = Color.FromArgb("#EF4444"), BorderWidth = 0, Padding = new Thickness(4), FontSize = 16 };
            removeBtn.Clicked += (_, _) => { _lineItems.RemoveAt(idx); RebuildLineItems(); };

            var prodBorder = new Border { Style = (Style)Resources["InputContainer"], HeightRequest = 42 };
            prodBorder.Content = prodPicker;
            var qtyBorder = new Border { Style = (Style)Resources["InputContainer"], HeightRequest = 42 };
            qtyBorder.Content = qtyEntry;
            var priceBorder = new Border { Style = (Style)Resources["InputContainer"], HeightRequest = 42 };
            priceBorder.Content = priceEntry;

            row.Add(prodBorder,  0, 0);
            row.Add(qtyBorder,   1, 0);
            row.Add(priceBorder, 2, 0);
            row.Add(removeBtn,   3, 0);
            LineItemsContainer.Children.Add(row);
        }
        UpdateTotals();
    }

    private void UpdateTotals()
    {
        decimal sub  = _lineItems.Sum(i => i.LineTotal);
        decimal disc = decimal.TryParse(EntryDiscount.Text, out var d) ? sub * d / 100 : 0;
        decimal tax  = decimal.TryParse(EntryTax.Text,      out var t) ? (sub - disc) * t / 100 : 0;
        decimal total = sub - disc + tax;
        LblSubtotal.Text   = $"${sub:N2}";
        LblDiscount.Text   = $"-${disc:N2}";
        LblTax.Text        = $"+${tax:N2}";
        LblGrandTotal.Text = $"${total:N2}";
    }

    private void OnTotalsChanged(object s, TextChangedEventArgs e) => UpdateTotals();

    private async void OnSaveClicked(object s, EventArgs e) => await SaveAsync(InvoiceStatus.Sent);
    private async void OnSaveDraftClicked(object s, EventArgs e) => await SaveAsync(InvoiceStatus.Draft);

    private async Task SaveAsync(InvoiceStatus status)
    {
        if (CustomerPicker.SelectedIndex < 0) { ShowError("Select a customer."); return; }
        if (_lineItems.Count == 0) { ShowError("Add at least one line item."); return; }

        var customer = _customerList[CustomerPicker.SelectedIndex];
        var inv      = _editing ?? new InvoiceModel { CompanyId = _company.CurrentCompany?.LocalId ?? Guid.Empty };
        inv.InvoiceNumber = LblInvoiceNumber.Text;
        inv.CustomerId    = customer.LocalId;
        inv.CustomerName  = customer.Name;
        //inv.InvoiceDate   = DateInvoice.Date;
        //inv.DueDate       = DateDue.Date;
        inv.Items         = [.._lineItems];
        inv.DiscountPct   = decimal.TryParse(EntryDiscount.Text, out var dp) ? dp : 0;
        inv.TaxPct        = decimal.TryParse(EntryTax.Text,      out var tp) ? tp : 0;
        inv.Notes         = EditorNotes.Text?.Trim() ?? "";
        inv.Status        = status;

        var (ok, err) = _editing == null ? await _billing.CreateAsync(inv) : await _billing.UpdateAsync(inv);
        if (ok) await Shell.Current.GoToAsync("..");
        else ShowError(err ?? "Save failed.");
    }

    private async void OnBackClicked(object s, EventArgs e) => await Shell.Current.GoToAsync("..");
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}

// Helper extension to resolve product by LocalId
file static class InvoiceFormExtensions
{
    public static Guid ProductId(this ProductModel p) => p.LocalId;
}
