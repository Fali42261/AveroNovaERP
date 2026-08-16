using AveroNova.App.UI.Layout;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class RegisterPage : ContentPage
{
    private readonly IAuthenticationService _auth;
    private readonly RegisterViewModel _vm;

    public RegisterPage(IAuthenticationService auth, RegisterViewModel vm)
    {
        InitializeComponent();
        _auth = auth;
        _vm = vm;
        BindingContext = _vm;
        SizeChanged += (_, _) => ApplyFieldLayout();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.StepChanged -= OnStepChanged;
        _vm.StepChanged += OnStepChanged;
        UpdateProgress();
        ApplyFieldLayout();
        UpdatePrimaryColumn();
    }

    protected override void OnDisappearing()
    {
        _vm.StepChanged -= OnStepChanged;
        base.OnDisappearing();
    }

    private void OnStepChanged(object? sender, EventArgs e)
    {
        UpdateProgress();
        ApplyFieldLayout();
        UpdatePrimaryColumn();
    }

    private void UpdatePrimaryColumn()
    {
        var width = Width;
        if (width <= 0)
            return;

        var size = ResponsiveBreakpoints.FromWidth(width);
        var usable = Math.Max(320, width - ResponsiveBreakpoints.PageGutter);
        var formWidth = Math.Min(usable, ResponsiveBreakpoints.FormMaxWidth(size));
        ApplyActionButtons(ResponsiveBreakpoints.FormColumnCount(formWidth, maxColumns: 3));
    }

    private void UpdateProgress()
    {
        PaintStep(StepDot1, StepNum1, 1);
        PaintStep(StepDot2, StepNum2, 2);
        PaintStep(StepDot3, StepNum3, 3);
        PaintStep(StepDot4, StepNum4, 4);
        PaintLine(StepLine1, 1);
        PaintLine(StepLine2, 2);
        PaintLine(StepLine3, 3);
    }

    private void PaintStep(Border dot, Label number, int step)
    {
        var current = _vm.CurrentStep;
        var primary = (Color)Microsoft.Maui.Controls.Application.Current!.Resources["PrimaryColor"];
        var white = (Color)Microsoft.Maui.Controls.Application.Current.Resources["White"];
        var gray = (Color)Microsoft.Maui.Controls.Application.Current.Resources["Gray200"];
        var muted = (Color)Microsoft.Maui.Controls.Application.Current.Resources["TextSecondary"];

        if (step < current)
        {
            dot.BackgroundColor = primary;
            number.TextColor = white;
            number.Text = "\u2713";
        }
        else if (step == current)
        {
            dot.BackgroundColor = primary;
            number.TextColor = white;
            number.Text = step.ToString();
        }
        else
        {
            dot.BackgroundColor = gray;
            number.TextColor = muted;
            number.Text = step.ToString();
        }
    }

    private void PaintLine(BoxView line, int fromStep)
    {
        var primary = (Color)Microsoft.Maui.Controls.Application.Current!.Resources["PrimaryColor"];
        var gray = (Color)Microsoft.Maui.Controls.Application.Current.Resources["Gray200"];
        line.Color = fromStep < _vm.CurrentStep ? primary : gray;
    }

    private void ApplyFieldLayout()
    {
        var width = Width;
        if (width <= 0)
            return;

        var size = ResponsiveBreakpoints.FromWidth(width);
        var usable = Math.Max(320, width - ResponsiveBreakpoints.PageGutter);
        var formWidth = Math.Min(usable, ResponsiveBreakpoints.FormMaxWidth(size));
        FormHost.WidthRequest = formWidth;
        FormHost.MaximumWidthRequest = formWidth;
        FormHost.HorizontalOptions = LayoutOptions.Center;

        var compactProgress = width < 700;
        StepLabel1.IsVisible = !compactProgress;
        StepLabel2.IsVisible = !compactProgress;
        StepLabel3.IsVisible = !compactProgress;
        StepLabel4.IsVisible = !compactProgress;
        StepDot1.WidthRequest = StepDot1.HeightRequest = compactProgress ? 28 : 32;
        StepDot2.WidthRequest = StepDot2.HeightRequest = compactProgress ? 28 : 32;
        StepDot3.WidthRequest = StepDot3.HeightRequest = compactProgress ? 28 : 32;
        StepDot4.WidthRequest = StepDot4.HeightRequest = compactProgress ? 28 : 32;

        var columns = ResponsiveBreakpoints.FormColumnCount(formWidth, maxColumns: 3);

        PlaceFlow(Step1Grid, columns, FullNameField, EmailField);
        PlaceCompanyFields(columns);
        PlaceFlow(Step3Grid, columns, PasswordField, ConfirmField);
        ApplyActionButtons(columns);
    }

    private void ApplyActionButtons(int columns)
    {
        var compact = columns <= 1;

        if (compact)
        {
            ActionGrid.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Star),
                new(GridLength.Star)
            };
            BackButton.HorizontalOptions = LayoutOptions.Fill;
            PrimaryActionButton.HorizontalOptions = LayoutOptions.Fill;
            Grid.SetColumn(PrimaryActionButton, _vm.IsBackVisible ? 1 : 0);
            Grid.SetColumnSpan(PrimaryActionButton, _vm.IsBackVisible ? 1 : 2);
        }
        else
        {
            ActionGrid.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Auto),
                new(GridLength.Star)
            };
            BackButton.HorizontalOptions = LayoutOptions.Start;
            PrimaryActionButton.HorizontalOptions = LayoutOptions.End;
            Grid.SetColumn(PrimaryActionButton, 1);
            Grid.SetColumnSpan(PrimaryActionButton, 1);
        }
    }

    private void PlaceCompanyFields(int columns)
    {
        View[] fields =
        [
            CompanyNameField, OwnerNameField, GSTNumberField,
            PANNumberField, CompanyEmailField, MobileNumberField,
            CountryField, StateField, CityField,
            PinCodeField, AddressField
        ];

        SetColumns(Step2Grid, columns);

        var index = 0;
        var maxRow = 0;
        foreach (var field in fields)
        {
            var column = index % columns;
            var row = index / columns;
            var span = 1;

            if (ReferenceEquals(field, AddressField) && columns > 1 && column < columns - 1)
                span = columns - column;

            Place(field, column, row, span);
            maxRow = Math.Max(maxRow, row);
            index += span;
        }

        SetRows(Step2Grid, maxRow + 1);
    }

    private static void PlaceFlow(Grid grid, int columns, params View[] fields)
    {
        SetColumns(grid, columns);
        for (var i = 0; i < fields.Length; i++)
            Place(fields[i], i % columns, i / columns);
        SetRows(grid, Math.Max(1, (int)Math.Ceiling(fields.Length / (double)columns)));
    }

    private static void SetRows(Grid grid, int count)
    {
        var rows = new RowDefinitionCollection();
        for (var i = 0; i < count; i++)
            rows.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions = rows;
    }

    private static void SetColumns(Grid grid, int count)
    {
        var columns = new ColumnDefinitionCollection();
        for (var i = 0; i < count; i++)
            columns.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions = columns;
    }

    private static void Place(View view, int column, int row, int columnSpan = 1)
    {
        Grid.SetColumn(view, column);
        Grid.SetRow(view, row);
        Grid.SetColumnSpan(view, columnSpan);
        Grid.SetRowSpan(view, 1);
    }

    private async void OnLoginTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.Login);

    private async void OnPrimaryActionClicked(object? sender, EventArgs e)
    {
        if (_vm.CurrentStep < 4)
        {
            _vm.NextCommand.Execute(null);
            return;
        }

        await CreateAccountAsync();
    }

    private async Task CreateAccountAsync()
    {
        if (!_vm.Validate())
            return;

        _vm.IsBusy = true;
        _vm.HasGeneralError = false;
        _vm.GeneralError = string.Empty;

        try
        {
            var (success, error) = await _auth.RegisterAsync(
                _vm.FullName.Trim(),
                _vm.Email.Trim(),
                _vm.Password);

            if (success)
            {
                _vm.Reset();
                await Shell.Current.GoToAsync(AppRoutes.OtpVerify);
            }
            else
            {
                _vm.HasGeneralError = true;
                _vm.GeneralError = error ?? "Registration failed. Please try again.";
            }
        }
        catch
        {
            _vm.HasGeneralError = true;
            _vm.GeneralError = "Something went wrong. Please try again.";
        }
        finally
        {
            _vm.IsBusy = false;
        }
    }
}
