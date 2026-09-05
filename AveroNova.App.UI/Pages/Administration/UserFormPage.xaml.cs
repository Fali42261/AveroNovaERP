using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Administration;

[QueryProperty(nameof(EditId), "id")]
public partial class UserFormPage : ContentPage
{
    private readonly IUserService _svc;
    private readonly ICompanyService _company;
    private UserModel? _editing;
    public string? EditId { get; set; }

    public UserFormPage(IUserService svc, ICompanyService company) { InitializeComponent(); _svc = svc; _company = company; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var roles = await _svc.GetAllRolesAsync();
        PickerRole.ItemsSource = roles;
        PickerRole.ItemDisplayBinding = new Binding("Name");

        if (!string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var id))
        {
            _editing = await _svc.GetByIdAsync(id);
            if (_editing != null)
            {
                LblTitle.Text = "Edit User";
                EntryFullName.Text = _editing.Name;
                EntryEmail.Text = _editing.Email;
                EntryPhone.Text = _editing.Phone;
                SwitchActive.IsToggled = _editing.Status == UserStatus.Active; 
                EditorNotes.Text = _editing.Notes;
                if (roles.Any(r => r.LocalId == _editing.RoleId)) PickerRole.SelectedItem = roles.First(r => r.LocalId == _editing.RoleId);
            }
        }
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryFullName.Text)) { ShowError("Full name is required."); return; }
        if (string.IsNullOrWhiteSpace(EntryEmail.Text)) { ShowError("Email is required."); return; }
        var model = _editing ?? new UserModel { CompanyId = _company.CurrentCompany?.LocalId ?? Guid.Empty };
        model.Name = EntryFullName.Text.Trim();
        model.Email = EntryEmail.Text.Trim();
        model.Phone = EntryPhone.Text?.Trim() ?? "";
        model.Status = SwitchActive.IsToggled
    ? UserStatus.Active
    : UserStatus.Inactive;
        model.Notes = EditorNotes.Text?.Trim() ?? "";
        if (PickerRole.SelectedItem is RoleModel selectedRole) model.RoleId = selectedRole.LocalId;
        var (ok, err) = _editing == null ? await _svc.CreateAsync(model) : await _svc.UpdateAsync(model);
        if (ok) await Shell.Current.GoToAsync("..");
        else ShowError(err ?? "Save failed.");
    }

    private async void OnBackClicked(object s, EventArgs e) => await Shell.Current.GoToAsync("..");
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
