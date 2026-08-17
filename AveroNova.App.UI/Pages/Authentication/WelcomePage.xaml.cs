using AveroNova.App.UI.Layout;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class WelcomePage : ContentPage
{
    private readonly IInstallationService _installation;
    private bool _layoutBusy;
    private bool? _appliedTwoColumn;
    private ScreenSize? _appliedSize;
    private double _appliedMinHeight = double.NaN;

    public WelcomePage(IInstallationService installation)
    {
        InitializeComponent();
        _installation = installation;
        SizeChanged += OnPageSizeChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ApplyLayout();
        await _installation.EnsureInitializedAsync();

        // Registered installs should not land here — redirect to Login.
        if (_installation.IsRegistered)
        {
            await Shell.Current.GoToAsync(AppRoutes.Login);
            return;
        }

        var canCreate = _installation.CanCreateAccount;
        OrDivider.IsVisible = canCreate;
        BtnCreateAccount.IsVisible = canCreate;
    }

    private void OnPageSizeChanged(object? sender, EventArgs e) => ApplyLayout();

    private void ApplyLayout()
    {
        if (_layoutBusy || Width <= 0)
            return;

        var size = ResponsiveBreakpoints.FromWidth(Width);
        var twoColumn = Width >= ResponsiveBreakpoints.ShellDesktopMinWidth;
        var metricsChanged = _appliedSize != size || _appliedTwoColumn != twoColumn;

        _layoutBusy = true;
        try
        {
            ApplyFillHeight();
            ApplyColumns(twoColumn);
            if (metricsChanged)
                ApplyMetrics(size, twoColumn);
        }
        finally
        {
            _layoutBusy = false;
        }
    }

    private void ApplyFillHeight()
    {
        if (Height <= 0)
            return;

        if (!double.IsNaN(_appliedMinHeight) && Math.Abs(_appliedMinHeight - Height) < 32)
            return;

        _appliedMinHeight = Height;
        RootLayout.MinimumHeightRequest = Height;
    }

    private void ApplyColumns(bool twoColumn)
    {
        if (_appliedTwoColumn == twoColumn)
            return;

        _appliedTwoColumn = twoColumn;

        if (twoColumn)
        {
            RootLayout.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Star),
                new(GridLength.Star)
            };
            RootLayout.RowDefinitions = new RowDefinitionCollection
            {
                new(GridLength.Star)
            };

            Grid.SetColumn(BrandPanel, 0);
            Grid.SetColumnSpan(BrandPanel, 1);
            Grid.SetRow(BrandPanel, 0);
            Grid.SetRowSpan(BrandPanel, 1);

            Grid.SetColumn(AuthPanel, 1);
            Grid.SetColumnSpan(AuthPanel, 1);
            Grid.SetRow(AuthPanel, 0);
            Grid.SetRowSpan(AuthPanel, 1);
        }
        else
        {
            RootLayout.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Star)
            };
            RootLayout.RowDefinitions = new RowDefinitionCollection
            {
                new(GridLength.Auto),
                new(GridLength.Star)
            };

            Grid.SetColumn(BrandPanel, 0);
            Grid.SetColumnSpan(BrandPanel, 1);
            Grid.SetRow(BrandPanel, 0);
            Grid.SetRowSpan(BrandPanel, 1);

            Grid.SetColumn(AuthPanel, 0);
            Grid.SetColumnSpan(AuthPanel, 1);
            Grid.SetRow(AuthPanel, 1);
            Grid.SetRowSpan(AuthPanel, 1);
        }
    }

    private void ApplyMetrics(ScreenSize size, bool twoColumn)
    {
        _appliedSize = size;

        BrandContent.Padding = size switch
        {
            ScreenSize.Compact => new Thickness(24, 32),
            ScreenSize.Medium => new Thickness(32),
            _ => new Thickness(48)
        };
        AuthContent.Padding = size switch
        {
            ScreenSize.Compact => new Thickness(24, 28),
            ScreenSize.Medium => new Thickness(28),
            _ => new Thickness(32)
        };

        BrandContent.Spacing = size == ScreenSize.Compact ? 16 : 20;
        FeatureList.Margin = size == ScreenSize.Compact ? new Thickness(0, 12, 0, 0) : new Thickness(0, 20, 0, 0);
        FeatureList.Spacing = size == ScreenSize.Compact ? 8 : 10;

        BrandContent.MaximumWidthRequest = twoColumn ? 420 : ResponsiveBreakpoints.FormMaxCompact;
        AuthContent.MaximumWidthRequest = twoColumn ? 440 : ResponsiveBreakpoints.FormMaxCompact;
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.Login);

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        await _installation.EnsureInitializedAsync();
        if (!_installation.CanCreateAccount)
        {
            await Shell.Current.GoToAsync(AppRoutes.Login);
            return;
        }

        await Shell.Current.GoToAsync(AppRoutes.Register);
    }
}
