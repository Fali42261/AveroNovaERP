using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Administration;

[QueryProperty(nameof(EditId), "id")]
public partial class RoleFormPage : ContentPage
{
    private readonly IUserService _svc;
    private RoleModel? _editing;
    public string? EditId { get; set; }

    public RoleFormPage(IUserService svc) { InitializeComponent(); _svc = svc; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var id))
        {
            _editing = await _svc.GetRoleByIdAsync(id);
            if (_editing != null)
            {
                LblTitle.Text = "Edit Role";
                EntryName.Text = _editing.Name;
                EditorDescription.Text = _editing.Description;
            }
        }
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryName.Text)) { ShowError("Role name is required."); return; }
        var model = _editing ?? new RoleModel();
        model.Name = EntryName.Text.Trim();
        model.Description = EditorDescription.Text?.Trim() ?? "";
        var (ok, err) = _editing == null ? await _svc.CreateRoleAsync(model) : await _svc.UpdateRoleAsync(model);
        if (ok) await Shell.Current.GoToAsync("..");
        else ShowError(err ?? "Save failed.");
    }

    private async void OnBackClicked(object s, EventArgs e) => await Shell.Current.GoToAsync("..");
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
