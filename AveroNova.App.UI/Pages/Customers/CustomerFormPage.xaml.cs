using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Customers;

[QueryProperty(nameof(EditId), "id")]
public partial class CustomerFormPage : ContentPage, IHostedPage
{
    private readonly ICustomerService _svc;
    private readonly ICompanyService _company;
    private readonly IMainContentNavigator _navigator;
    private CustomerModel? _editing;

    public string? EditId { get; set; }

    public CustomerFormPage(
        ICustomerService svc,
        ICompanyService company,
        IMainContentNavigator navigator)
    {
        InitializeComponent();
        _svc = svc;
        _company = company;
        _navigator = navigator;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadForHostAsync();
    }

    public async Task LoadForHostAsync()
    {
        try
        {
            ErrorBanner.IsVisible = false;
            if (!string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var id))
            {
                _editing = await _svc.GetByIdAsync(id);
                if (_editing != null)
                {
                    LblTitle.Text = "Edit Customer";
                    EntryName.Text = _editing.Name;
                    EntryEmail.Text = _editing.Email;
                    EntryPhone.Text = _editing.Phone;
                    EntryAddress.Text = _editing.Address;
                    EntryCity.Text = _editing.City;
                    EntryCountry.Text = _editing.Country;
                    EntryTax.Text = _editing.TaxNumber;
                    EditorNotes.Text = _editing.Notes;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CustomerForm] Load failed: {ex}");
            ShowError("Customer details could not be loaded. Please try again.");
        }
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryName.Text))
        {
            ShowError("Customer name is required.");
            return;
        }

        try
        {
            var companyId = _company.CurrentCompany?.LocalId ?? Guid.Empty;
            if (companyId == Guid.Empty)
            {
                ShowError("No company is selected. Please select a company first.");
                return;
            }

            var model = _editing ?? new CustomerModel { CompanyId = companyId };
            model.CompanyId = companyId;
            model.Name = EntryName.Text.Trim();
            model.Email = EntryEmail.Text?.Trim() ?? string.Empty;
            model.Phone = EntryPhone.Text?.Trim() ?? string.Empty;
            model.Address = EntryAddress.Text?.Trim() ?? string.Empty;
            model.City = EntryCity.Text?.Trim() ?? string.Empty;
            model.Country = EntryCountry.Text?.Trim() ?? string.Empty;
            model.TaxNumber = EntryTax.Text?.Trim() ?? string.Empty;
            model.Notes = EditorNotes.Text?.Trim() ?? string.Empty;

            var (ok, err) = _editing == null
                ? await _svc.CreateAsync(model)
                : await _svc.UpdateAsync(model);

            if (ok)
            {
                await _navigator.GoBackAsync();
                return;
            }

            ShowError(err ?? "Save failed.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CustomerForm] Save failed: {ex}");
            ShowError("Customer could not be saved. The app is still running; please try again.");
        }
    }

    private async void OnBackClicked(object s, EventArgs e) => await _navigator.GoBackAsync();

    private void ShowError(string msg)
    {
        LblError.Text = msg;
        ErrorBanner.IsVisible = true;
    }
}
