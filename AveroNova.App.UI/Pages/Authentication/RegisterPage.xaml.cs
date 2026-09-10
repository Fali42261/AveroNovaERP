using AveroNova.App.UI.Layout;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class RegisterPage : ContentPage
{
    private readonly IAuthenticationService _auth;
    private readonly IInstallationService _installation;
    private readonly RegisterViewModel _vm;
    private bool _layoutBusy;
    private ScreenSize? _appliedSize;
    private int _appliedColumns = -1;
    private int _lastStep = -1;

    public RegisterPage(IAuthenticationService auth, IInstallationService installation, RegisterViewModel vm)
    {
        InitializeComponent();
        _auth = auth;
        _installation = installation;
        _vm = vm;
        BindingContext = _vm;
        SizeChanged += (_, _) => ApplyLayout();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RegisterViewModel.CurrentStep))
            {
                UpdateStepUi();
                ApplyLayout();
            }
            else if (e.PropertyName == nameof(RegisterViewModel.PlanOptions))
            {
                ApplyLayout();
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _installation.EnsureInitializedAsync();
        UpdateStepUi();
        ApplyLayout();
        if (_vm.CurrentStep < 1 || _vm.CurrentStep > 4)
            _vm.CurrentStep = 1;
        await _vm.LoadPlansAsync();
        UpdateStepUi();
        ApplyLayout();
    }

    private void UpdateStepUi()
    {
        PersonalGrid.IsVisible = _vm.IsStep1;
        CompanyGrid.IsVisible = _vm.IsStep2;
        SubscriptionHost.IsVisible = _vm.IsStep3;
        ReviewHost.IsVisible = _vm.IsStep4;
        BtnBack.IsVisible = _vm.CanGoBack;
        LoginLink.IsVisible = _vm.ShowLoginLink;
        UpdateProgress();
    }

    private void ApplyLayout()
    {
        if (_layoutBusy || Width <= 0)
            return;

        _layoutBusy = true;
        try
        {
            var size = ResponsiveBreakpoints.FromWidth(Width);
            var compact = size == ScreenSize.Compact;

            ContentHost.Padding = size switch
            {
                ScreenSize.Compact => new Thickness(20, 24),
                ScreenSize.Medium => new Thickness(32, 28),
                _ => new Thickness(40, 36)
            };

            AuthCard.HorizontalOptions = LayoutOptions.Center;
            AuthCard.Padding = compact ? new Thickness(4, 8) : new Thickness(32);

            if (compact)
            {
                AuthCard.StrokeThickness = 0;
                AuthCard.BackgroundColor = Colors.Transparent;
            }
            else
            {
                AuthCard.ClearValue(Border.StrokeThicknessProperty);
                AuthCard.ClearValue(Border.BackgroundColorProperty);
            }

            var available = Math.Max(280, Width - ContentHost.Padding.HorizontalThickness);
            var max = compact
                ? ResponsiveBreakpoints.FormMaxCompact
                : ResponsiveBreakpoints.FormMaxWidth(size);
            var cardWidth = Math.Min(available, max);
            if (Math.Abs(AuthCard.WidthRequest - cardWidth) >= 1)
            {
                AuthCard.WidthRequest = cardWidth;
                AuthCard.MaximumWidthRequest = cardWidth;
            }

            var innerWidth = Math.Max(240, cardWidth - AuthCard.Padding.HorizontalThickness);
            var columns = ResponsiveBreakpoints.FormColumnCount(innerWidth, 3);
            if (compact)
                columns = 1;

            if (_appliedColumns != columns || _appliedSize != size || _lastStep != _vm.CurrentStep)
            {
                _appliedColumns = columns;
                _appliedSize = size;
                _lastStep = _vm.CurrentStep;
                LayoutPersonal(columns);
                LayoutCompany(columns);
            }

            LayoutPlans(compact);
            LayoutReview(compact);
            LayoutActions(compact);
        }
        finally
        {
            _layoutBusy = false;
        }
    }

    private void LayoutPersonal(int columns)
    {
        View[][] rows =
        [
            [FieldFullName, FieldEmail, FieldMobile],
            [FieldCity, FieldState, FieldCountry],
            [FieldPin, FieldPassword, FieldConfirmPassword],
            [FieldAddress]
        ];
        ApplyFieldGrid(PersonalGrid, rows, columns);
    }

    private void LayoutCompany(int columns)
    {
        View[][] rows =
        [
            [FieldCompanyName, FieldOwnerName, FieldGst],
            [FieldPan, FieldCompanyEmail, FieldCompanyMobile],
            [FieldCompanyCountry, FieldCompanyState, FieldCompanyCity],
            [FieldCompanyPin],
            [FieldCompanyAddress]
        ];
        ApplyFieldGrid(CompanyGrid, rows, columns);
    }

    private void LayoutPlans(bool compact)
    {
        var children = PlansGrid.Children.OfType<View>().ToList();
        if (children.Count == 0)
            return;

        foreach (var child in children)
            child.HeightRequest = -1;

        PlansGrid.ColumnDefinitions.Clear();
        PlansGrid.RowDefinitions.Clear();

        if (compact)
        {
            PlansGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (var i = 0; i < children.Count; i++)
            {
                PlansGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetRow(children[i], i);
                Grid.SetColumn(children[i], 0);
                Grid.SetColumnSpan(children[i], 1);
                children[i].HorizontalOptions = LayoutOptions.Fill;
                children[i].VerticalOptions = LayoutOptions.Fill;
                children[i].WidthRequest = -1;
                children[i].MaximumWidthRequest = double.PositiveInfinity;
            }
            return;
        }

        var cols = Math.Min(3, Math.Max(1, children.Count));
        for (var c = 0; c < cols; c++)
            PlansGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        PlansGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var i = 0; i < children.Count; i++)
        {
            Grid.SetRow(children[i], 0);
            Grid.SetColumn(children[i], i);
            Grid.SetColumnSpan(children[i], 1);
            children[i].HorizontalOptions = LayoutOptions.Fill;
            children[i].VerticalOptions = LayoutOptions.Fill;
            children[i].WidthRequest = -1;
            children[i].MaximumWidthRequest = double.PositiveInfinity;
        }

        Dispatcher.Dispatch(() => Dispatcher.Dispatch(() => EqualizeHeights(children)));
    }

    private void LayoutReview(bool compact)
    {
        View[] cards = [ReviewPersonal, ReviewCompany, ReviewSubscription, ReviewAdditional];
        foreach (var card in cards)
            card.HeightRequest = -1;

        ReviewHost.ColumnDefinitions.Clear();
        ReviewHost.RowDefinitions.Clear();

        if (compact)
        {
            ReviewHost.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (var i = 0; i < cards.Length; i++)
            {
                ReviewHost.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetRow(cards[i], i);
                Grid.SetColumn(cards[i], 0);
                Grid.SetColumnSpan(cards[i], 1);
                cards[i].HorizontalOptions = LayoutOptions.Fill;
                cards[i].VerticalOptions = LayoutOptions.Fill;
            }
            return;
        }

        for (var c = 0; c < 4; c++)
            ReviewHost.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ReviewHost.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var i = 0; i < cards.Length; i++)
        {
            Grid.SetRow(cards[i], 0);
            Grid.SetColumn(cards[i], i);
            Grid.SetColumnSpan(cards[i], 1);
            cards[i].HorizontalOptions = LayoutOptions.Fill;
            cards[i].VerticalOptions = LayoutOptions.Fill;
        }

        Dispatcher.Dispatch(() => Dispatcher.Dispatch(() => EqualizeHeights(cards)));
    }

    private static void EqualizeHeights(IReadOnlyList<View> views)
    {
        double max = 0;
        foreach (var view in views)
            max = Math.Max(max, view.Height);

        if (max <= 1)
            return;

        foreach (var view in views)
        {
            if (Math.Abs(view.HeightRequest - max) >= 1)
                view.HeightRequest = max;
        }
    }

    private void LayoutActions(bool compact)
    {
        BtnBack.IsVisible = _vm.CanGoBack;
        ActionsHost.HorizontalOptions = LayoutOptions.End;
        BtnBack.HorizontalOptions = LayoutOptions.Fill;
        BtnPrimary.HorizontalOptions = LayoutOptions.Fill;
        BtnBack.WidthRequest = compact ? 104 : 120;
        BtnPrimary.WidthRequest = compact ? 132 : 168;
    }

    private static void ApplyFieldGrid(Grid grid, View[][] rows, int columns)
    {
        columns = Math.Clamp(columns, 1, 3);
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();

        if (columns == 1)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var r = 0;
            foreach (var row in rows)
            {
                foreach (var view in row)
                {
                    grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    Grid.SetRow(view, r);
                    Grid.SetColumn(view, 0);
                    Grid.SetColumnSpan(view, 1);
                    r++;
                }
            }
            return;
        }

        for (var c = 0; c < columns; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        var gridRow = 0;
        foreach (var row in rows)
        {
            if (row.Length == 1)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetRow(row[0], gridRow);
                Grid.SetColumn(row[0], 0);
                Grid.SetColumnSpan(row[0], columns);
                gridRow++;
                continue;
            }

            var chunks = (int)Math.Ceiling(row.Length / (double)columns);
            for (var chunk = 0; chunk < chunks; chunk++)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                for (var c = 0; c < columns; c++)
                {
                    var index = chunk * columns + c;
                    if (index >= row.Length)
                        break;
                    var view = row[index];
                    Grid.SetRow(view, gridRow);
                    Grid.SetColumn(view, c);
                    Grid.SetColumnSpan(view, 1);
                }
                gridRow++;
            }
        }
    }

    private void UpdateProgress()
    {
        PaintStep(1, Badge1, Badge1Label, Prog1Label);
        PaintStep(2, Badge2, Badge2Label, Prog2Label);
        PaintStep(3, Badge3, Badge3Label, Prog3Label);
        PaintStep(4, Badge4, Badge4Label, Prog4Label);
    }

    private void PaintStep(int step, Border badge, Label badgeLabel, Label caption)
    {
        var current = _vm.CurrentStep;
        Color primary = Color.FromArgb("#2563EB");
        Color complete = Color.FromArgb("#059669");
        Color idle = Color.FromArgb("#E5E7EB");
        Color textIdle = Color.FromArgb("#64748B");

        if (step == current)
        {
            badge.BackgroundColor = primary;
            badgeLabel.TextColor = Colors.White;
            caption.FontAttributes = FontAttributes.Bold;
            caption.TextColor = primary;
        }
        else if (step < current)
        {
            badge.BackgroundColor = complete;
            badgeLabel.TextColor = Colors.White;
            caption.FontAttributes = FontAttributes.None;
            caption.TextColor = complete;
        }
        else
        {
            badge.BackgroundColor = idle;
            badgeLabel.TextColor = textIdle;
            caption.FontAttributes = FontAttributes.None;
            caption.TextColor = textIdle;
        }
    }

    private async void OnPrimaryClicked(object? sender, EventArgs e)
    {
        if (_vm.CurrentStep < 4)
        {
            var step = _vm.CurrentStep;
            _vm.NextCommand.Execute(null);
            if (_vm.CurrentStep == step)
                await FocusFirstInvalidFieldAsync();
            return;
        }

        if (!_vm.ValidateForm())
        {
            await FocusFirstInvalidFieldAsync();
            return;
        }

        _vm.IsBusy = true;
        _vm.HasGeneralError = false;
        _vm.GeneralError = string.Empty;
        _vm.HasGeneralSuccess = false;
        _vm.GeneralSuccess = string.Empty;

        try
        {
            var (success, error) = await _auth.RegisterAsync(new AveroNova.Application.DTOs.Auth.RegisterRequest
            {
                FullName = _vm.FullName.Trim(),
                Email = _vm.Email.Trim(),
                MobileNumber = _vm.Mobile.Trim(),
                Password = _vm.Password,
                ConfirmPassword = _vm.ConfirmPassword,
                CompanyName = _vm.CompanyName.Trim(),
                OwnerName = string.IsNullOrWhiteSpace(_vm.OwnerName) ? null : _vm.OwnerName.Trim(),
                CompanyEmail = _vm.CompanyEmail.Trim(),
                CompanyMobile = _vm.CompanyMobile.Trim(),
                GstNumber = _vm.GSTNumber?.Trim(),
                PanNumber = _vm.PanNumber?.Trim(),
                CompanyAddress = _vm.CompanyAddress?.Trim(),
                CompanyCity = _vm.CompanyCity?.Trim(),
                CompanyState = _vm.CompanyState?.Trim(),
                CompanyCountry = _vm.CompanyCountry?.Trim(),
                CompanyPinCode = _vm.CompanyPinCode?.Trim(),
                Plan = _vm.SelectedPlanOption?.Name ?? "Starter"
            });

            if (success)
            {
                _vm.HasGeneralSuccess = true;
                _vm.GeneralSuccess = "Account created successfully! Redirecting to sign in...";
                await AppToast.ShowAsync(this, "Account created successfully.", AppToastKind.Success);
                await Task.Delay(1200);
                await Shell.Current.GoToAsync(AppRoutes.Login);
            }
            else
            {
                _vm.HasGeneralError = true;
                _vm.GeneralError = error ?? "Registration failed. Please try again.";
                await AppToast.ShowAsync(this, _vm.GeneralError, AppToastKind.Error);
            }
        }
        catch (Exception ex)
        {
            _vm.HasGeneralError = true;
            _vm.GeneralError = ex.Message;
            await AppToast.ShowAsync(this, "Account could not be created. Please try again.", AppToastKind.Error);
        }
        finally
        {
            _vm.IsBusy = false;
        }
    }

    private async Task FocusFirstInvalidFieldAsync()
    {
        View? host = _vm.CurrentStep switch
        {
            1 when _vm.HasFullNameError => FieldFullName,
            1 when _vm.HasEmailError => FieldEmail,
            1 when _vm.HasMobileError => FieldMobile,
            1 when _vm.HasPasswordError => FieldPassword,
            1 when _vm.HasConfirmPasswordError => FieldConfirmPassword,
            2 when _vm.HasCompanyNameError => FieldCompanyName,
            2 when _vm.HasOwnerNameError => FieldOwnerName,
            2 when _vm.HasCompanyEmailError => FieldCompanyEmail,
            2 when _vm.HasCompanyMobileError => FieldCompanyMobile,
            _ => null
        };

        if (host == null)
            return;

        var input = FindFocusableInput(host);
        input?.Focus();
        await PageScroll.ScrollToAsync(host, ScrollToPosition.MakeVisible, true);
    }

    private static View? FindFocusableInput(View root)
    {
        if (root is Entry or Editor)
            return root;

        if (root is Border border && border.Content is View borderContent)
            return FindFocusableInput(borderContent);

        if (root is Microsoft.Maui.Controls.Layout layout)
        {
            foreach (var child in layout.Children)
            {
                if (child is View view)
                {
                    var found = FindFocusableInput(view);
                    if (found != null)
                        return found;
                }
            }
        }

        return null;
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        if (_vm.CurrentStep > 1)
            _vm.BackCommand.Execute(null);
    }

    private async void OnLoginTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
