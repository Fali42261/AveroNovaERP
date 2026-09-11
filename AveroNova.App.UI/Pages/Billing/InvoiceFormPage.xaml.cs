using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Pages.Customers;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Billing;

[QueryProperty(nameof(EditId), "id")]
public partial class InvoiceFormPage : ContentPage, IHostedPage
{
    private const string AddCustomerOption = "+ Add new customer";

    private readonly IBillingService _billing;
    private readonly ICustomerService _customers;
    private readonly IProductService _products;
    private readonly ICompanyService _company;
    private readonly ISettingsService _settings;
    private readonly IMainContentNavigator _navigator;
    private readonly IServiceProvider _services;

    private List<CustomerModel> _customerList = [];
    private List<ProductModel> _productList = [];
    private readonly List<InvoiceLineItem> _lineItems = [];
    private InvoiceModel? _editing;
    private string _currencySymbol = "₹";
    private bool _loading;
    private bool _customerPickerBusy;

    public string? EditId { get; set; }

    public InvoiceFormPage(
        IBillingService billing,
        ICustomerService customers,
        IProductService products,
        ICompanyService company,
        ISettingsService settings,
        IMainContentNavigator navigator,
        IServiceProvider services)
    {
        InitializeComponent();
        _billing = billing;
        _customers = customers;
        _products = products;
        _company = company;
        _settings = settings;
        _navigator = navigator;
        _services = services;
        CustomerPicker.SelectedIndexChanged += OnCustomerChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadForHostAsync();
    }

    public async Task LoadForHostAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            ErrorBanner.IsVisible = false;
            var cid = _company.CurrentCompany?.LocalId ?? Guid.Empty;
            if (cid == Guid.Empty)
            {
                ShowError("Select a company before creating an invoice.");
                return;
            }

            var appSettings = await _settings.GetAsync();
            _currencySymbol = string.IsNullOrWhiteSpace(appSettings.CurrencySymbol)
                ? (appSettings.Currency == "INR" ? "₹" : "$" )
                : appSettings.CurrencySymbol;

            _customerList = await _customers.GetAllAsync(cid);
            _productList = await _products.GetAllAsync(cid);
            RefreshCustomerPicker();

            if (_editing is null && string.IsNullOrWhiteSpace(EditId))
            {
                LblInvoiceNumber.Text = await _billing.GetNextInvoiceNumberAsync(cid);
                DateInvoice.Date = DateTime.Today;
                DateDue.Date = DateTime.Today.AddDays(30);
                PaymentMethodPicker.SelectedIndex = 0;
            }
            else if (_editing is null && Guid.TryParse(EditId, out var id))
            {
                _editing = await _billing.GetByIdAsync(id);
                if (_editing is not null) PopulateForm(_editing);
            }

            UpdateTotals();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InvoiceForm] Load failed: {ex}");
            ShowError("Invoice could not be loaded. Please try again.");
        }
        finally
        {
            _loading = false;
        }
    }

    private void RefreshCustomerPicker(Guid? selectedCustomerId = null)
    {
        _customerPickerBusy = true;
        try
        {
            var items = _customerList.Select(c => c.Name).ToList();
            items.Add(AddCustomerOption);
            CustomerPicker.ItemsSource = items;

            var wantedId = selectedCustomerId ?? _editing?.CustomerId;
            if (wantedId is Guid id)
            {
                var index = _customerList.FindIndex(c => c.LocalId == id);
                CustomerPicker.SelectedIndex = index;
            }
        }
        finally
        {
            _customerPickerBusy = false;
        }
    }

    private async void OnCustomerChanged(object? sender, EventArgs e)
    {
        if (_customerPickerBusy) return;
        if (CustomerPicker.SelectedIndex != _customerList.Count) return;

        _customerPickerBusy = true;
        CustomerPicker.SelectedIndex = -1;
        _customerPickerBusy = false;

        try
        {
            var page = ActivatorUtilities.CreateInstance<CustomerFormPage>(_services);
            await _navigator.NavigateAsync(page, "Add Customer", "Home / Billing / New Invoice / Add Customer");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InvoiceForm] Customer navigation failed: {ex}");
            ShowError("Customer form could not be opened.");
        }
    }

    private void PopulateForm(InvoiceModel inv)
    {
        LblTitle.Text = "Edit Invoice";
        LblInvoiceNumber.Text = inv.InvoiceNumber;
        DateInvoice.Date = inv.InvoiceDate;
        DateDue.Date = inv.DueDate;
        RefreshCustomerPicker(inv.CustomerId);
        EntryDiscount.Text = inv.DiscountPct.ToString("0.##");
        EntryTax.Text = inv.TaxPct.ToString("0.##");
        EditorNotes.Text = inv.Notes;
        PaymentMethodPicker.SelectedIndex = Math.Max(0, (int)inv.PaymentMethod);

        _lineItems.Clear();
        _lineItems.AddRange(inv.Items.Select(x => new InvoiceLineItem
        {
            ProductId = x.ProductId,
            ProductName = x.ProductName,
            SKU = x.SKU,
            UnitPrice = x.UnitPrice,
            Quantity = x.Quantity,
            DiscountPct = x.DiscountPct,
            TaxPct = x.TaxPct
        }));
        RebuildLineItems();
    }

    private void OnAddLineItem(object? sender, EventArgs e)
    {
        try
        {
            _lineItems.Add(new InvoiceLineItem { Quantity = 1, UnitPrice = 0m });
            RebuildLineItems();
            UpdateTotals();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InvoiceForm] Add item failed: {ex}");
            ShowError("Item could not be added.");
        }
    }

    private void RebuildLineItems()
    {
        LineItemsContainer.Children.Clear();

        if (_lineItems.Count == 0)
        {
            LineItemsContainer.Children.Add(new Label
            {
                Text = "No items added yet.",
                FontSize = 12,
                TextColor = Color.FromArgb("#64748B"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 8)
            });
            UpdateTotals();
            return;
        }

        for (var i = 0; i < _lineItems.Count; i++)
        {
            var item = _lineItems[i];
            var index = i;

            var stack = new VerticalStackLayout { Spacing = 8 };
            var productPicker = new Picker
            {
                Title = "Select product",
                ItemsSource = _productList.Select(p => p.Name).ToList(),
                BackgroundColor = Colors.Transparent
            };
            var productIndex = _productList.FindIndex(p => p.LocalId == item.ProductId);
            if (productIndex >= 0) productPicker.SelectedIndex = productIndex;

            productPicker.SelectedIndexChanged += (_, _) =>
            {
                if (productPicker.SelectedIndex < 0 || productPicker.SelectedIndex >= _productList.Count) return;
                var product = _productList[productPicker.SelectedIndex];
                item.ProductId = product.LocalId;
                item.ProductName = product.Name;
                item.SKU = product.SKU ?? string.Empty;
                item.UnitPrice = product.SellingPrice;
                item.TaxPct = product.TaxPercent;
                RebuildLineItems();
                UpdateTotals();
            };

            stack.Children.Add(FieldBorder(productPicker));

            var values = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection(
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)),
                ColumnSpacing = 8
            };

            var qty = new Entry { Text = Math.Max(1, item.Quantity).ToString(), Keyboard = Keyboard.Numeric, Placeholder = "Qty" };
            var rate = new Entry { Text = item.UnitPrice.ToString("0.00"), Keyboard = Keyboard.Numeric, Placeholder = "Rate" };
            var amount = new Label
            {
                Text = Money(item.GrandTotal),
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.End
            };
            var remove = new Button
            {
                Text = "✕",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#EF4444"),
                BorderWidth = 0,
                Padding = new Thickness(6),
                WidthRequest = 40
            };

            qty.TextChanged += (_, _) =>
            {
                if (int.TryParse(qty.Text, out var q) && q > 0)
                {
                    item.Quantity = q;
                    amount.Text = Money(item.GrandTotal);
                    UpdateTotals();
                }
            };
            rate.TextChanged += (_, _) =>
            {
                if (decimal.TryParse(rate.Text, out var p) && p >= 0)
                {
                    item.UnitPrice = p;
                    amount.Text = Money(item.GrandTotal);
                    UpdateTotals();
                }
            };
            remove.Clicked += (_, _) =>
            {
                if (index >= 0 && index < _lineItems.Count)
                {
                    _lineItems.RemoveAt(index);
                    RebuildLineItems();
                    UpdateTotals();
                }
            };

            values.Add(FieldBorder(qty), 0, 0);
            values.Add(FieldBorder(rate), 1, 0);
            values.Add(amount, 2, 0);
            values.Add(remove, 3, 0);
            stack.Children.Add(values);

            LineItemsContainer.Children.Add(new Border
            {
                Stroke = Color.FromArgb("#E2E8F0"),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
                Padding = new Thickness(10),
                Content = stack
            });
        }

        UpdateTotals();
    }

    private static Border FieldBorder(View content) => new()
    {
        Stroke = Color.FromArgb("#CBD5E1"),
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
        Padding = new Thickness(8, 2),
        Content = content
    };

    private void UpdateTotals()
    {
        var sub = _lineItems.Sum(i => i.LineTotal);
        var discPct = decimal.TryParse(EntryDiscount.Text, out var d) ? Math.Clamp(d, 0m, 100m) : 0m;
        var taxPct = decimal.TryParse(EntryTax.Text, out var t) ? Math.Clamp(t, 0m, 100m) : 0m;
        var disc = sub * discPct / 100m;
        var lineTax = _lineItems.Sum(i => i.TaxAmount);
        var headerTax = sub * taxPct / 100m;
        var tax = lineTax + headerTax;
        var total = Math.Max(0m, sub + tax - disc);

        LblSubtotal.Text = Money(sub);
        LblDiscount.Text = $"-{Money(disc)}";
        LblTax.Text = $"+{Money(tax)}";
        var formattedTotal = Money(total);
        LblGrandTotal.Text = formattedTotal;
        LblFooterGrandTotal.Text = formattedTotal;
    }

    private string Money(decimal amount) => $"{_currencySymbol}{amount:N2}";
    private void OnTotalsChanged(object? sender, TextChangedEventArgs e) => UpdateTotals();

    private async void OnSaveClicked(object? sender, EventArgs e) => await SaveAsync();

    private async Task SaveAsync()
    {
        ErrorBanner.IsVisible = false;
        if (CustomerPicker.SelectedIndex < 0 || CustomerPicker.SelectedIndex >= _customerList.Count)
        {
            ShowError("Select a customer.");
            return;
        }
        if (_lineItems.Count == 0 || _lineItems.Any(i => i.ProductId == Guid.Empty || i.Quantity <= 0))
        {
            ShowError("Add at least one valid product item.");
            return;
        }

        try
        {
            BtnSave.IsEnabled = false;
            BtnSave.Text = "Saving...";

            var customer = _customerList[CustomerPicker.SelectedIndex];
            var inv = _editing ?? new InvoiceModel { CompanyId = _company.CurrentCompany?.LocalId ?? Guid.Empty };
            inv.InvoiceNumber = LblInvoiceNumber.Text;
            inv.CustomerId = customer.LocalId;
            inv.CustomerName = customer.Name;
            inv.InvoiceDate = DateInvoice.Date ?? DateTime.Today;
            inv.DueDate = DateDue.Date ?? inv.InvoiceDate.AddDays(30);
            inv.Items = [.. _lineItems];
            inv.DiscountPct = decimal.TryParse(EntryDiscount.Text, out var dp) ? Math.Clamp(dp, 0m, 100m) : 0m;
            inv.TaxPct = decimal.TryParse(EntryTax.Text, out var tp) ? Math.Clamp(tp, 0m, 100m) : 0m;
            inv.PaymentMethod = PaymentMethodPicker.SelectedIndex >= 0
                ? (PaymentMethod)PaymentMethodPicker.SelectedIndex
                : PaymentMethod.Cash;
            inv.Notes = EditorNotes.Text?.Trim() ?? string.Empty;
            inv.Status = InvoiceStatus.Draft;

            var (ok, error) = _editing is null
                ? await _billing.CreateAsync(inv)
                : await _billing.UpdateAsync(inv);

            if (ok) await _navigator.GoBackAsync();
            else ShowError(error ?? "Invoice could not be saved.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InvoiceForm] Save failed: {ex}");
            ShowError("Invoice could not be saved. Your app remains open.");
        }
        finally
        {
            BtnSave.IsEnabled = true;
            BtnSave.Text = "Save";
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e) => await _navigator.GoBackAsync();

    private void ShowError(string message)
    {
        LblError.Text = message;
        ErrorBanner.IsVisible = true;
    }
}
