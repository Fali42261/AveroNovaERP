using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Navigation;

namespace AveroNova.App.UI.Pages.Administration;

[QueryProperty(nameof(EditId), "id")]
public partial class UserFormPage : ContentPage, IHostedPage
{
    private readonly IUserService _svc;
    private readonly ICompanyService _company;
    private readonly IMainContentNavigator _navigator;
    private UserModel? _editing;
    public string? EditId { get; set; }

    public UserFormPage(IUserService svc, ICompanyService company, IMainContentNavigator navigator) { InitializeComponent(); _svc = svc; _company = company; _navigator=navigator; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadForHostAsync();
    }
    public async Task LoadForHostAsync()
    {
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
        if (ok) await _navigator.GoBackAsync();
        else ShowError(err ?? "Save failed.");
    }

    private async void OnBackClicked(object s, EventArgs e) => await _navigator.GoBackAsync();
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
