using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Purchases;

public partial class SupplierFormPage : ContentPage, IHostedPage
{
    private readonly ISupplierService _service;
    private readonly ICompanyService _company;
    private readonly IMainContentNavigator _nav;
    private SupplierModel? _editing;
    private bool _loading;
    private bool _saving;
    public Guid? EditId { get; set; }

    public SupplierFormPage(ISupplierService service, ICompanyService company, IMainContentNavigator nav)
    {
        InitializeComponent();
        _service = service;
        _company = company;
        _nav = nav;
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
            if (EditId.HasValue && _editing is null)
            {
                _editing = await _service.GetByIdAsync(EditId.Value);
                if (_editing is not null)
                {
                    LblTitle.Text = "Edit Supplier";
                    EntryName.Text = _editing.Name;
                    EntryEmail.Text = _editing.Email;
                    EntryPhone.Text = _editing.Phone;
                    EntryAddress.Text = _editing.Address;
                    EntryTaxNumber.Text = _editing.TaxNumber;
                    EditorNotes.Text = _editing.Notes;
                    ActiveSwitch.IsToggled = _editing.IsActive;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SupplierForm] Load failed: {ex}");
            ShowError("Supplier could not be loaded.");
        }
        finally
        {
            _loading = false;
        }
    }

    private async void OnSaveClicked(object? s, EventArgs e)
    {
        if (_saving) return;
        ErrorBanner.IsVisible = false;

        if (string.IsNullOrWhiteSpace(EntryName.Text))
        {
            ShowError("Supplier name is required.");
            return;
        }

        var companyId = _company.CurrentCompany?.LocalId ?? Guid.Empty;
        if (companyId == Guid.Empty)
        {
            ShowError("No company is selected.");
            return;
        }

        _saving = true;
        try
        {
            var model = _editing ?? new SupplierModel { CompanyId = companyId };
            model.CompanyId = companyId;
            model.Name = EntryName.Text.Trim();
            model.Email = EntryEmail.Text?.Trim() ?? string.Empty;
            model.Phone = EntryPhone.Text?.Trim() ?? string.Empty;
            model.Address = EntryAddress.Text?.Trim() ?? string.Empty;
            model.TaxNumber = EntryTaxNumber.Text?.Trim() ?? string.Empty;
            model.Notes = EditorNotes.Text?.Trim() ?? string.Empty;
            model.IsActive = ActiveSwitch.IsToggled;

            var result = _editing is null
                ? await _service.CreateAsync(model)
                : await _service.UpdateAsync(model);

            if (!result.Ok)
            {
                ShowError(result.Error ?? "Save failed.");
                return;
            }

            await _nav.GoBackAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SupplierForm] Save failed: {ex}");
            ShowError("Supplier could not be saved. Please try again.");
        }
        finally
        {
            _saving = false;
        }
    }

    private async void OnBackClicked(object? s, EventArgs e)
    {
        try { await _nav.GoBackAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SupplierForm] Back failed: {ex}"); }
    }

    private void ShowError(string message)
    {
        LblError.Text = message;
        ErrorBanner.IsVisible = true;
    }
}
