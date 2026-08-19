using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Profile;

public partial class UserProfilePage : ContentPage
{
    private readonly IAuthenticationService _auth;

    public UserProfilePage(IAuthenticationService auth)
    {
        InitializeComponent();
        _auth = auth;
    }

    public void Reload()
    {
        var user = _auth.CurrentUser;
        LblName.Text = user?.Name ?? "Unknown user";
        LblEmail.Text = user?.Email ?? string.Empty;
        LblInitials.Text = string.IsNullOrWhiteSpace(user?.AvatarInitials) ? "AN" : user.AvatarInitials;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Reload();
    }
}
