using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Security;
using AveroNova.Shared.Security;

namespace AveroNova.App.UI.Pages.Administration;

[QueryProperty(nameof(EditId), "id")]
public partial class UserFormPage : ContentPage, IHostedPage
{
    private readonly IUserService _svc;
    private readonly ICompanyService _company;
    private readonly IMainContentNavigator _navigator;
    private readonly ILocalCredentialStore _credentials;
    private readonly Pbkdf2PasswordHasher _hasher = new();
    private UserModel? _editing;
    private bool _saving;
    public string? EditId { get; set; }

    public UserFormPage(
        IUserService svc,
        ICompanyService company,
        IMainContentNavigator navigator,
        ILocalCredentialStore credentials)
    {
        InitializeComponent();
        _svc = svc;
        _company = company;
        _navigator = navigator;
        _credentials = credentials;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadForHostAsync();
    }

    public async Task LoadForHostAsync()
    {
        ErrorBanner.IsVisible = false;
        var roles = await _svc.GetAllRolesAsync();
        PickerRole.ItemsSource = roles;
        PickerRole.ItemDisplayBinding = new Binding("Name");

        if (!string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var id))
        {
            _editing = await _svc.GetByIdAsync(id);
            if (_editing != null)
            {
                LblTitle.Text = "Edit User";
                BtnSave.Text = "Save User";
                LoginPasswordSection.IsVisible = false;
                EntryFullName.Text = _editing.Name;
                EntryEmail.Text = _editing.Email;
                EntryPhone.Text = _editing.Phone;
                SwitchActive.IsToggled = _editing.Status == UserStatus.Active;
                EditorNotes.Text = _editing.Notes;
                if (roles.Any(r => r.LocalId == _editing.RoleId))
                    PickerRole.SelectedItem = roles.First(r => r.LocalId == _editing.RoleId);
            }
        }
        else
        {
            LblTitle.Text = "Add User";
            BtnSave.Text = "Create User";
            LoginPasswordSection.IsVisible = true;
        }
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (_saving) return;
        ErrorBanner.IsVisible = false;

        if (string.IsNullOrWhiteSpace(EntryFullName.Text)) { ShowError("Full name is required."); return; }
        if (string.IsNullOrWhiteSpace(EntryEmail.Text)) { ShowError("Company / work email is required."); return; }
        if (PickerRole.SelectedItem is not RoleModel) { ShowError("Select a role."); return; }

        var isNew = _editing is null;
        if (isNew)
        {
            var password = EntryPassword.Text ?? string.Empty;
            var confirm = EntryConfirmPassword.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(password)) { ShowError("Login password is required."); return; }
            if (!PasswordPolicy.IsStrong(password)) { ShowError(PasswordPolicy.RequirementMessage); return; }
            if (!string.Equals(password, confirm, StringComparison.Ordinal)) { ShowError("Passwords do not match."); return; }
        }

        _saving = true;
        BtnSave.IsEnabled = false;
        try
        {
            var model = _editing ?? new UserModel { CompanyId = _company.CurrentCompany?.LocalId ?? Guid.Empty };
            model.Name = EntryFullName.Text.Trim();
            model.Email = EntryEmail.Text.Trim().ToLowerInvariant();
            model.Phone = EntryPhone.Text?.Trim() ?? string.Empty;
            model.Status = SwitchActive.IsToggled ? UserStatus.Active : UserStatus.Inactive;
            model.Notes = EditorNotes.Text?.Trim() ?? string.Empty;
            if (PickerRole.SelectedItem is RoleModel selectedRole) model.RoleId = selectedRole.LocalId;

            var (ok, err) = isNew
                ? await _svc.CreateAsync(model)
                : await _svc.UpdateAsync(model);

            if (!ok)
            {
                ShowError(err ?? "Save failed.");
                return;
            }

            if (isNew)
            {
                try
                {
                    var password = EntryPassword.Text ?? string.Empty;
                    await _credentials.SetPasswordHashAsync(
                        model.LocalId,
                        model.Email,
                        _hasher.HashPassword(password));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UserForm] Credential save failed: {ex}");
                    await _svc.DeleteAsync(model.LocalId);
                    ShowError("User login credentials could not be saved securely. Please try again.");
                    return;
                }
            }

            await _navigator.GoBackAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UserForm] Save failed: {ex}");
            ShowError("User could not be saved. Please try again.");
        }
        finally
        {
            _saving = false;
            BtnSave.IsEnabled = true;
        }
    }

    private async void OnBackClicked(object s, EventArgs e) => await _navigator.GoBackAsync();
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
