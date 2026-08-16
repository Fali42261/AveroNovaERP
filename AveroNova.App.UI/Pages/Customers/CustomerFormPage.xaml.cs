using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Customers;

[QueryProperty(nameof(EditId), "id")]
public partial class CustomerFormPage : ContentPage
{
    private readonly ICustomerService _svc;
    private readonly ICompanyService  _company;
    private CustomerModel? _editing;
    public string? EditId { get; set; }

    public CustomerFormPage(ICustomerService svc, ICompanyService company) { InitializeComponent(); _svc = svc; _company = company; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var id))
        {
            _editing = await _svc.GetByIdAsync(id);
            if (_editing != null)
            {
                LblTitle.Text      = "Edit Customer";
                EntryName.Text     = _editing.Name;
                EntryEmail.Text    = _editing.Email;
                EntryPhone.Text    = _editing.Phone;
                EntryAddress.Text  = _editing.Address;
                EntryCity.Text     = _editing.City;
                EntryCountry.Text  = _editing.Country;
                EntryTax.Text      = _editing.TaxNumber;
                EditorNotes.Text   = _editing.Notes;
            }
        }
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryName.Text)) { ShowError("Customer name is required."); return; }
        var model      = _editing ?? new CustomerModel { CompanyId = _company.CurrentCompany?.LocalId ?? Guid.Empty };
        model.Name     = EntryName.Text.Trim();
        model.Email    = EntryEmail.Text?.Trim() ?? "";
        model.Phone    = EntryPhone.Text?.Trim() ?? "";
        model.Address  = EntryAddress.Text?.Trim() ?? "";
        model.City     = EntryCity.Text?.Trim() ?? "";
        model.Country  = EntryCountry.Text?.Trim() ?? "";
        model.TaxNumber= EntryTax.Text?.Trim() ?? "";
        model.Notes    = EditorNotes.Text?.Trim() ?? "";
        var (ok, err)  = _editing == null ? await _svc.CreateAsync(model) : await _svc.UpdateAsync(model);
        if (ok) await Shell.Current.GoToAsync("..");
        else ShowError(err ?? "Save failed.");
    }

    private async void OnBackClicked(object s, EventArgs e) => await Shell.Current.GoToAsync("..");
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
