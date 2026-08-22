using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.Local;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Domain.Constants;

namespace AveroNova.App.UI.Pages.Administration;

[QueryProperty(nameof(EditId), "id")]
public partial class UserFormPage : ContentPage
{
    private static readonly string[] StatusOptions = ["Active", "Inactive"];

    private readonly IUserService _svc;
    private readonly IToastService _toasts;
    private readonly CurrentAccessService _access;
    private UserModel? _editing;
    private List<RoleModel> _roles = [];
    private bool _saving;
    private bool _canAssignRole = true;

    public string? EditId { get; set; }

    public UserFormPage(IUserService svc, IToastService toasts, CurrentAccessService access)
    {
        InitializeComponent();
        _svc = svc;
        _toasts = toasts;
        _access = access;
        PickerStatus.ItemsSource = StatusOptions;
        PickerStatus.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _toasts.AttachTo(this);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        LoadingState.IsVisible = true;
        FormScroll.IsVisible = false;
        ErrorBanner.IsVisible = false;
        try
        {
            var snapshot = await _access.GetSnapshotAsync();
            var isEdit = Guid.TryParse(EditId, out var id);
            var required = isEdit ? PermissionNames.UsersUpdate : PermissionNames.UsersCreate;
            if (!PermissionNames.Grants(snapshot.Permissions, required))
            {
                ShowError("You do not have permission to continue.");
                BtnSave.IsEnabled = false;
                return;
            }

            _canAssignRole = PermissionNames.Grants(snapshot.Permissions, PermissionNames.UsersAssignRole);
            _roles = await _svc.GetAssignableRolesAsync();
            PickerRole.ItemsSource = _roles;
            PickerRole.ItemDisplayBinding = new Binding(nameof(RoleModel.Name));
            PickerRole.IsEnabled = _canAssignRole;

            if (isEdit)
            {
                _editing = await _svc.GetByIdAsync(id);
                if (_editing == null)
                {
                    ShowError("User not found.");
                    BtnSave.IsEnabled = false;
                    return;
                }

                if (_editing.IsOwner)
                {
                    ShowError("The company owner cannot be edited.");
                    BtnSave.IsEnabled = false;
                    return;
                }

                LblTitle.Text = "Edit User";
                PasswordSection.IsVisible = false;
                EntryFullName.Text = _editing.Name;
                EntryEmail.Text = _editing.Email;
                EntryPhone.Text = _editing.Phone;
                PickerStatus.SelectedItem = _editing.Status == UserStatus.Inactive ? "Inactive" : "Active";
                var match = _roles.FirstOrDefault(r => r.LocalId == _editing.RoleId);
                if (match != null)
                    PickerRole.SelectedItem = match;
            }
            else
            {
                LblTitle.Text = "Add User";
                PasswordSection.IsVisible = true;
            }
        }
        finally
        {
            LoadingState.IsVisible = false;
            FormScroll.IsVisible = true;
        }
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (_saving)
            return;

        ErrorBanner.IsVisible = false;
        if (string.IsNullOrWhiteSpace(EntryFullName.Text)) { ShowError("Full name is required."); return; }
        if (string.IsNullOrWhiteSpace(EntryEmail.Text)) { ShowError("Email is required."); return; }
        if (string.IsNullOrWhiteSpace(EntryPhone.Text)) { ShowError("Mobile number is required."); return; }
        if (PickerRole.SelectedItem is not RoleModel selectedRole)
        {
            ShowError("Role is required.");
            return;
        }

        if (_editing == null)
        {
            if (string.IsNullOrWhiteSpace(EntryPassword.Text)) { ShowError("Password is required."); return; }
            if (string.IsNullOrWhiteSpace(EntryConfirmPassword.Text)) { ShowError("Confirm password is required."); return; }
            if (!string.Equals(EntryPassword.Text, EntryConfirmPassword.Text, StringComparison.Ordinal))
            {
                ShowError("Passwords do not match.");
                return;
            }
        }

        _saving = true;
        BtnSave.IsEnabled = false;
        BtnSave.Text = "Saving...";
        try
        {
            var model = _editing ?? new UserModel();
            model.Name = EntryFullName.Text.Trim();
            model.Email = EntryEmail.Text.Trim();
            model.Phone = EntryPhone.Text.Trim();
            model.RoleId = selectedRole.LocalId;
            model.Status = string.Equals(PickerStatus.SelectedItem as string, "Inactive", StringComparison.OrdinalIgnoreCase)
                ? UserStatus.Inactive
                : UserStatus.Active;
            model.Password = _editing == null ? EntryPassword.Text : null;

            var (ok, err) = _editing == null
                ? await _svc.CreateAsync(model)
                : await _svc.UpdateAsync(model);

            if (!ok)
            {
                var message = err ?? "Unable to save user.";
                ShowError(message);
                _toasts.ShowError(message, string.Empty);
                return;
            }

            UserChangeNotifier.Notify(
                _editing == null ? "User created successfully." : "User updated successfully.");
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            _saving = false;
            BtnSave.IsEnabled = true;
            BtnSave.Text = "Save User";
        }
    }

    private async void OnBackClicked(object s, EventArgs e)
    {
        if (_saving)
            return;
        await Shell.Current.GoToAsync("..");
    }

    private void ShowError(string msg)
    {
        LblError.Text = msg;
        ErrorBanner.IsVisible = true;
    }
}
