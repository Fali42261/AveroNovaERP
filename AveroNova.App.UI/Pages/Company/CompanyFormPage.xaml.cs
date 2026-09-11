using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Company;

[QueryProperty(nameof(EditId), "id")]
public partial class CompanyFormPage : ContentPage, IHostedPage
{
    private readonly ICompanyService _svc;
    private readonly IMainContentNavigator _navigator;
    private CompanyModel? _editing;
    private bool _saving;
    private bool _loaded;

    public string? EditId { get; set; }

    public CompanyFormPage(ICompanyService svc, IMainContentNavigator navigator)
    {
        InitializeComponent();
        _svc = svc;
        _navigator = navigator;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadForHostAsync();
    }

    public async Task LoadForHostAsync()
    {
        if (_loaded && _editing is not null) return;
        try
        {
            if (!string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var id))
            {
                _editing = await _svc.GetByIdAsync(id);
                if (_editing != null)
                {
                    LblTitle.Text = "Edit Company";
                    EntryName.Text = _editing.Name;
                    EntryEmail.Text = _editing.Email;
                    EntryPhone.Text = _editing.Phone;
                    EntryWebsite.Text = _editing.Website;
                    EntryAddress.Text = _editing.Address;
                    EntryCity.Text = _editing.City;
                    EntryCountry.Text = _editing.Country;
                    EntryTax.Text = _editing.TaxNumber;
                    EntryRegNo.Text = _editing.RegistrationNo;
                    EntryCurrency.Text = _editing.Currency;
                    EntryInvPrefix.Text = _editing.InvoicePrefix;
                }
            }
            _loaded = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CompanyForm] Load failed: {ex}");
            ShowError("Company details could not be loaded.");
        }
    }

    private async void OnSaveClicked(object? s, EventArgs e)
    {
        if (_saving) return;
        if (string.IsNullOrWhiteSpace(EntryName.Text))
        {
            ShowError("Company name is required.");
            return;
        }

        _saving = true;
        ErrorBanner.IsVisible = false;
        try
        {
            var model = _editing ?? new CompanyModel();
            model.Name = EntryName.Text.Trim();
            model.Email = EntryEmail.Text?.Trim() ?? "";
            model.Phone = EntryPhone.Text?.Trim() ?? "";
            model.Website = EntryWebsite.Text?.Trim() ?? "";
            model.Address = EntryAddress.Text?.Trim() ?? "";
            model.City = EntryCity.Text?.Trim() ?? "";
            model.Country = EntryCountry.Text?.Trim() ?? "";
            model.TaxNumber = EntryTax.Text?.Trim() ?? "";
            model.RegistrationNo = EntryRegNo.Text?.Trim() ?? "";
            model.Currency = EntryCurrency.Text?.Trim() is { Length: > 0 } c ? c : "INR";
            model.InvoicePrefix = EntryInvPrefix.Text?.Trim() is { Length: > 0 } p ? p : "INV";

            var (ok, error) = _editing == null
                ? await _svc.CreateAsync(model)
                : await _svc.UpdateAsync(model);

            if (!ok)
            {
                ShowError(error ?? "Save failed.");
                return;
            }

            await _navigator.GoBackAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CompanyForm] Save failed: {ex}");
            ShowError("Company could not be saved. Please try again.");
        }
        finally { _saving = false; }
    }

    private async void OnBackClicked(object? s, EventArgs e)
    {
        try { await _navigator.GoBackAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CompanyForm] Back failed: {ex}"); }
    }

    private void ShowError(string msg)
    {
        LblError.Text = msg;
        ErrorBanner.IsVisible = true;
    }
}
