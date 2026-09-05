using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Navigation;

namespace AveroNova.App.UI.Pages.Administration;

[QueryProperty(nameof(EditId), "id")]
public partial class RoleFormPage : ContentPage, IHostedPage
{
    private readonly IUserService _svc;
    private RoleModel? _editing;
    private readonly IMainContentNavigator _navigator;private readonly List<(PermissionModel Permission,CheckBox Box)> _checks=[];
    public string? EditId { get; set; }

    public RoleFormPage(IUserService svc, IMainContentNavigator navigator) { InitializeComponent(); _svc = svc; _navigator=navigator; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadForHostAsync();
    }
    public async Task LoadForHostAsync()
    {
        if (!string.IsNullOrEmpty(EditId) && _editing is null && Guid.TryParse(EditId, out var id))
        {
            _editing = await _svc.GetRoleByIdAsync(id);
            if (_editing != null)
            {
                LblTitle.Text = "Edit Role";
                EntryName.Text = _editing.Name;
                EditorDescription.Text = _editing.Description;
            }
        }
        PermissionsList.Children.Clear();_checks.Clear();foreach(var p in await _svc.GetPermissionsAsync()){var box=new CheckBox{IsChecked=_editing?.Permissions.Contains(p.Key)==true};var row=new HorizontalStackLayout{Spacing=8};row.Children.Add(box);row.Children.Add(new Label{Text=$"{p.Module}: {p.Label}",VerticalOptions=LayoutOptions.Center});PermissionsList.Children.Add(row);_checks.Add((p,box));}
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryName.Text)) { ShowError("Role name is required."); return; }
        var model = _editing ?? new RoleModel();
        model.Name = EntryName.Text.Trim();
        model.Description = EditorDescription.Text?.Trim() ?? "";
        model.Permissions=_checks.Where(x=>x.Box.IsChecked).Select(x=>x.Permission.Key).ToList();
        var (ok, err) = _editing == null ? await _svc.CreateRoleAsync(model) : await _svc.UpdateRoleAsync(model);
        if (ok) await _navigator.GoBackAsync();
        else ShowError(err ?? "Save failed.");
    }

    private async void OnBackClicked(object s, EventArgs e) => await _navigator.GoBackAsync();
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
