using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Purchases;

public partial class SupplierFormPage : ContentPage, IHostedPage
{
    private readonly ISupplierService _service;
    private readonly ICompanyService _company;
    private readonly IMainContentNavigator _nav;
    private SupplierModel? _editing;
    public Guid? EditId { get; set; }

    public SupplierFormPage(ISupplierService service, ICompanyService company, IMainContentNavigator nav)
    { InitializeComponent(); _service = service; _company = company; _nav = nav; }

    protected override async void OnAppearing() { base.OnAppearing(); await LoadForHostAsync(); }

    public async Task LoadForHostAsync()
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

    private async void OnSaveClicked(object s, EventArgs e)
    {
        ErrorBanner.IsVisible = false;
        var isNew = _editing is null;
        var model = _editing ?? new SupplierModel { CompanyId = _company.CurrentCompany?.LocalId ?? Guid.Empty };
        model.Name = EntryName.Text?.Trim() ?? "";
        model.Email = EntryEmail.Text?.Trim() ?? "";
        model.Phone = EntryPhone.Text?.Trim() ?? "";
        model.Address = EntryAddress.Text?.Trim() ?? "";
        model.TaxNumber = EntryTaxNumber.Text?.Trim() ?? "";
        model.Notes = EditorNotes.Text?.Trim() ?? "";
        model.IsActive = ActiveSwitch.IsToggled;
        var result = isNew ? await _service.CreateAsync(model) : await _service.UpdateAsync(model);
        if (!result.Ok)
        {
            var message = result.Error ?? "Unable to save supplier.";
            LblError.Text = message;
            ErrorBanner.IsVisible = true;
            await AppToast.ErrorAsync(message);
            return;
        }
        await AppToast.SuccessAsync(isNew ? "Supplier created successfully." : "Supplier updated successfully.");
        await _nav.GoBackAsync();
    }

    private async void OnBackClicked(object s, EventArgs e) => await _nav.GoBackAsync();
}
