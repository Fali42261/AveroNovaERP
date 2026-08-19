using AveroNova.App.UI.Layout;
using AveroNova.App.UI.Navigation;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class WelcomePage : ContentPage
{
    private bool _layoutBusy;
    private bool? _appliedTwoColumn;
    private ScreenSize? _appliedSize;
    private double _appliedMinHeight = double.NaN;

    public WelcomePage()
    {
        InitializeComponent();
        SizeChanged += OnPageSizeChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLayout();
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

        // Binding min-height to Height on every SizeChanged re-enters layout
        // (scrollbar / viewport oscillation) and freezes the Windows UI thread.
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

    private bool _navBusy;

    private async void OnLoginClicked(object? sender, EventArgs e)
        => await NavigateBusyAsync(BtnSignIn, "Please wait...", AppRoutes.Login);

    private async void OnRegisterClicked(object? sender, EventArgs e)
        => await NavigateBusyAsync(BtnCreateAccount, "Please wait...", AppRoutes.Register);

    private async Task NavigateBusyAsync(Button button, string busyText, string route)
    {
        if (_navBusy)
            return;

        _navBusy = true;
        var original = button.Text;
        button.Text = busyText;
        BtnSignIn.IsEnabled = false;
        BtnCreateAccount.IsEnabled = false;
        WelcomeSpinner.IsVisible = true;
        WelcomeSpinner.IsRunning = true;
        try
        {
            await Shell.Current.GoToAsync(route);
        }
        finally
        {
            WelcomeSpinner.IsVisible = false;
            WelcomeSpinner.IsRunning = false;
            button.Text = original;
            BtnSignIn.IsEnabled = true;
            BtnCreateAccount.IsEnabled = true;
            _navBusy = false;
        }
    }
}
