using AveroNova.App.UI.Controls.Common;
using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Resources;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Administration;

[QueryProperty(nameof(UserId), "id")]
public partial class UserViewPage : ContentPage, IHostedPage
{
    private readonly IUserService _svc;
    private UserModel? _user;
    private readonly IMainContentNavigator _navigator;private readonly Func<UserFormPage> _formFactory;

    public string? UserId { get; set; }

    public UserViewPage(IUserService svc, IMainContentNavigator navigator, Func<UserFormPage> formFactory)
    {
        InitializeComponent();
        _svc = svc;
        _navigator=navigator;_formFactory=formFactory;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await LoadForHostAsync();
    }
    public async Task LoadForHostAsync()
    {
        if (!string.IsNullOrEmpty(UserId) &&
            Guid.TryParse(UserId, out var id))
        {
            _user = await _svc.GetByIdAsync(id);

            if (_user != null)
                BuildContent(_user);
        }
    }

    private void BuildContent(UserModel u)
    {
        Content.Children.Clear();

        bool isActive = u.Status == UserStatus.Active;

        // ---------------------------------------------------------
        // Profile Card
        // ---------------------------------------------------------

        var profileCard = new Border
        {
            Style = (Style)Resources["AppCard"]
        };

        var pGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)),
            ColumnSpacing = 16
        };

        var av = new Border
        {
            WidthRequest = 60,
            HeightRequest = 60,
            BackgroundColor = Color.FromArgb("#EFF6FF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(30)
            }
        };

        av.Content = new Label
        {
            Text = u.AvatarInitials,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2563EB"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var info = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center
        };

        info.Children.Add(new Label
        {
            Text = u.Name,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        });

        info.Children.Add(new Label
        {
            Text = u.Email,
            FontSize = 13,
            TextColor = Color.FromArgb("#64748B")
        });

        info.Children.Add(new Label
        {
            Text = u.Phone,
            FontSize = 13,
            TextColor = Color.FromArgb("#64748B")
        });

        var statusBadge = new Border
        {
            BackgroundColor = isActive
                ? Color.FromArgb("#ECFDF5")
                : Color.FromArgb("#FEF2F2"),

            StrokeThickness = 0,

            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(999)
            },

            Padding = new Thickness(8, 3),
            HorizontalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 4, 0, 0)
        };

        statusBadge.Content = new Label
        {
            Text = u.StatusLabel,
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,

            TextColor = isActive
                ? Color.FromArgb("#059669")
                : Color.FromArgb("#DC2626")
        };

        info.Children.Add(statusBadge);

        pGrid.Add(av, 0, 0);
        pGrid.Add(info, 1, 0);

        profileCard.Content = pGrid;

        // ---------------------------------------------------------
        // Details Card
        // ---------------------------------------------------------

        var detailCard = new Border
        {
            Style = (Style)Resources["AppCard"]
        };

        var dVsl = new VerticalStackLayout
        {
            Spacing = 12
        };

        dVsl.Children.Add(new Label
        {
            Text = "Details",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        });

        dVsl.Children.Add(new BoxView
        {
            Style = (Style)Resources["Divider"]
        });

        void AddDetail(string label, string value)
        {
            var g = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection(
                    new ColumnDefinition(new GridLength(130)),
                    new ColumnDefinition(GridLength.Star))
            };

            g.Add(
                new Label
                {
                    Text = label,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#64748B")
                },
                0,
                0);

            g.Add(
                new Label
                {
                    Text = value,
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold
                },
                1,
                0);

            dVsl.Children.Add(g);
        }

        AddDetail("Role", u.Role);
        AddDetail("Email", u.Email);
        AddDetail("Phone", u.Phone);
        AddDetail("Status", u.StatusLabel);
        AddDetail("Joined", u.CreatedAt.ToString("dd MMM yyyy"));

        if (!string.IsNullOrWhiteSpace(u.CompanyName))
            AddDetail("Company", u.CompanyName);

        if (u.LastLoginAt.HasValue)
            AddDetail("Last Login", u.LastLoginDisplay);

        detailCard.Content = dVsl;

        // ---------------------------------------------------------
        // Add Cards
        // ---------------------------------------------------------

        Content.Children.Add(profileCard);
        Content.Children.Add(detailCard);
    }

    private async void OnEditClicked(
        object sender,
        EventArgs e)
    {
        if (_user == null)
            return;

        var page=_formFactory();page.EditId=_user.LocalId.ToString("D");await _navigator.NavigateAsync(page,"Edit User","Home / Users / Edit");
    }

    private async void OnBackClicked(
        object sender,
        EventArgs e)
    {
        await _navigator.GoBackAsync();
    }

    private async void OnDeleteClicked(
        object sender,
        EventArgs e)
    {
        if (_user == null)
            return;

        if (!await DialogHelper.ConfirmDeleteAsync(
                "User",
                $"Delete {_user.Name}?"))
        {
            return;
        }

        var result=await _svc.DeleteAsync(_user.LocalId);if(!result.Ok){await DisplayAlert("Delete failed",result.Error??"Unable to delete user.","OK");return;}
        await _navigator.GoBackAsync();
    }
}
