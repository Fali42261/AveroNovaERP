using AveroNova.App.UI.Controls.Cards;
using AveroNova.App.UI.Controls.Forms;
using AveroNova.App.UI.Layout;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class RegisterPage : ContentPage
{
    private readonly IAuthenticationService _auth;
    private readonly IToastService _toasts;
    private readonly RegisterViewModel _vm;

    public RegisterPage(IAuthenticationService auth, RegisterViewModel vm, IToastService toasts)
    {
        InitializeComponent();
        _auth = auth;
        _vm = vm;
        _toasts = toasts;
        BindingContext = _vm;
        SizeChanged += (_, _) =>
        {
            if (!_equalizingReviewHeights && !_equalizingPlanHeights)
                ApplyFieldLayout();
        };
    }

    private readonly HashSet<string> _step1TouchedFields = [];
    private readonly HashSet<string> _step2TouchedFields = [];
    private bool _step1InteractionReady;
    private bool _step2InteractionReady;
    private bool _step1UnfocusedAttached;
    private bool _step2UnfocusedAttached;
    private bool _equalizingReviewHeights;
    private bool _equalizingPlanHeights;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.StepChanged -= OnStepChanged;
        _vm.StepChanged += OnStepChanged;
        NavigationPage.SetHasBackButton(this, false);
        Shell.SetNavBarIsVisible(this, false);
        _step1InteractionReady = false;
        _step2InteractionReady = false;
        _vm.PrepareStep1Display();
        AttachStep1FieldValidation();
        AttachStep2FieldValidation();
        UpdateProgress();
        ApplyFieldLayout();
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(400), () =>
        {
            _step1InteractionReady = true;
            _step2InteractionReady = true;
        });
    }

    protected override void OnDisappearing()
    {
        _vm.StepChanged -= OnStepChanged;
        base.OnDisappearing();
    }

    private void AttachStep1FieldValidation()
    {
        if (_step1UnfocusedAttached)
            return;

        _step1UnfocusedAttached = true;
        AttachStep1Field(FullNameField, "FullName");
        AttachStep1Field(EmailField, "Email");
        AttachStep1Field(MobileField, "Mobile");
        AttachStep1Field(PasswordField, "Password");
        AttachStep1Field(ConfirmField, "ConfirmPassword");
    }

    private void AttachStep1Field(Microsoft.Maui.Controls.Layout host, string field)
    {
        var entry = FindEntry(host);
        if (entry is null)
            return;

        entry.Focused += (_, _) =>
        {
            if (_step1InteractionReady)
                _step1TouchedFields.Add(field);
        };
        entry.Unfocused += (_, _) =>
        {
            if (_step1InteractionReady && _step1TouchedFields.Contains(field))
                _vm.ValidateStep1FieldAfterInteraction(field);
        };
    }

    private void AttachStep2FieldValidation()
    {
        if (_step2UnfocusedAttached)
            return;

        _step2UnfocusedAttached = true;
        AttachStep2Field(CompanyNameField, "CompanyName");
        AttachStep2Field(OwnerNameField, "OwnerName");
        AttachStep2Field(CompanyEmailField, "CompanyEmail");
        AttachStep2Field(MobileNumberField, "MobileNumber");
    }

    private void AttachStep2Field(Microsoft.Maui.Controls.Layout host, string field)
    {
        var entry = FindEntry(host);
        if (entry is null)
            return;

        entry.Focused += (_, _) =>
        {
            if (_step2InteractionReady)
                _step2TouchedFields.Add(field);
        };
        entry.Unfocused += (_, _) =>
        {
            if (_step2InteractionReady && _step2TouchedFields.Contains(field))
                _vm.ValidateStep2FieldAfterInteraction(field);
        };
    }

    private static Entry? FindEntry(IView view)
    {
        switch (view)
        {
            case Entry entry:
                return entry;
            case Border { Content: IView content }:
                return FindEntry(content);
            case ContentView { Content: IView inner }:
                return FindEntry(inner);
            case Microsoft.Maui.Controls.Layout layout:
                foreach (var child in layout.Children)
                {
                    if (child is IView childView)
                    {
                        var found = FindEntry(childView);
                        if (found is not null)
                            return found;
                    }
                }
                break;
        }

        return null;
    }

    private void OnStepChanged(object? sender, EventArgs e)
    {
        if (_vm.CurrentStep == 2)
        {
            _step2InteractionReady = false;
            _vm.PrepareStep2Display();
            AttachStep2FieldValidation();
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(400), () => _step2InteractionReady = true);
        }

        UpdateProgress();
        ApplyFieldLayout();
        if (_vm.CurrentStep == 3)
            Dispatcher.Dispatch(ArrangePlanCards);
        if (_vm.CurrentStep == 4)
            Dispatcher.Dispatch(ArrangeReviewCards);
        _ = PageScroll.ScrollToAsync(0, 0, false);
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

        // Keep the step digit. Replacing it with U+2713 rendered as an unexpected "Q"
        // because OpenSansRegular does not contain that glyph.
        number.Text = step.ToString();

        if (step <= current)
        {
            dot.BackgroundColor = primary;
            number.TextColor = white;
        }
        else
        {
            dot.BackgroundColor = gray;
            number.TextColor = muted;
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
        var compact = size == ScreenSize.Compact;
        var usable = Math.Max(320, width - ResponsiveBreakpoints.PageGutter);
        var formWidth = Math.Min(usable, ResponsiveBreakpoints.FormMaxWidth(size));
        if ((_vm.CurrentStep == 3 || _vm.CurrentStep == 4) && !compact)
            formWidth = usable;

        FormHost.WidthRequest = formWidth;
        FormHost.MaximumWidthRequest = formWidth;
        FormHost.HorizontalOptions = LayoutOptions.Center;
        FormHost.VerticalOptions = LayoutOptions.Start;
        FormHost.Padding = compact
            ? new Thickness(20, 24, 20, 180)
            : new Thickness(24, 32, 24, 48);

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

        PlaceFlow(
            Step1Grid,
            columns,
            FullNameField, EmailField, MobileField,
            PersonalPinField, PasswordField, ConfirmField,
            PersonalAddressField, PersonalCityField, PersonalStateField,
            PersonalCountryField);
        PlaceCompanyFields(columns);
        ArrangePlanCards();
        ArrangeReviewCards();
        ApplyReviewFieldLayout();
        AlignActionButtons();
    }

    private void AlignActionButtons()
    {
        var compact = Width > 0 && ResponsiveBreakpoints.FromWidth(Width) == ScreenSize.Compact;
        var stackButtons = compact && Width > 0 && Width < ResponsiveBreakpoints.FormMaxCompact;
        var buttonHeight = ResourceDouble("ButtonHeight", 48);
        var backMin = ResourceDouble("WizardButtonMinWidth", 140);
        var primaryMin = ResourceDouble("WizardPrimaryButtonMinWidth", 180);

        ActionGrid.HorizontalOptions = LayoutOptions.Fill;
        ActionButtonsHost.ColumnSpacing = compact ? 8 : 12;
        ActionButtonsHost.RowSpacing = 8;

        ApplyWizardButtonSize(BackButton, buttonHeight, backMin);
        ApplyWizardButtonSize(PrimaryActionButton, buttonHeight, primaryMin);

        if (stackButtons)
        {
            ActionButtonsHost.HorizontalOptions = LayoutOptions.Fill;
            ActionButtonsHost.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Star)
            };
            ActionButtonsHost.RowDefinitions = new RowDefinitionCollection
            {
                new(GridLength.Auto),
                new(GridLength.Auto),
                new(GridLength.Auto)
            };
            Place(BackButton, 0, 0);
            Place(ActionSpinner, 0, 1);
            Place(PrimaryActionButton, 0, 2);
            BackButton.HorizontalOptions = LayoutOptions.Fill;
            PrimaryActionButton.HorizontalOptions = LayoutOptions.Fill;
            ActionSpinner.HorizontalOptions = LayoutOptions.Center;
        }
        else
        {
            ActionButtonsHost.HorizontalOptions = LayoutOptions.End;
            ActionButtonsHost.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new(GridLength.Auto),
                new(GridLength.Auto),
                new(GridLength.Auto)
            };
            ActionButtonsHost.RowDefinitions = new RowDefinitionCollection
            {
                new(GridLength.Auto)
            };
            Place(BackButton, 0, 0);
            Place(ActionSpinner, 1, 0);
            Place(PrimaryActionButton, 2, 0);
            BackButton.HorizontalOptions = LayoutOptions.Start;
            PrimaryActionButton.HorizontalOptions = LayoutOptions.Start;
            ActionSpinner.HorizontalOptions = LayoutOptions.Center;
        }

        ActionSpinner.VerticalOptions = LayoutOptions.Center;
        BackButton.VerticalOptions = LayoutOptions.Center;
        PrimaryActionButton.VerticalOptions = LayoutOptions.Center;
    }

    private static void ApplyWizardButtonSize(Button button, double height, double minWidth)
    {
        button.HeightRequest = height;
        button.MinimumHeightRequest = height;
        button.MinimumWidthRequest = minWidth;
        button.Padding = new Thickness(20, 0);
        button.FontSize = 14;
        button.FontAttributes = FontAttributes.Bold;
        button.CornerRadius = 10;
        button.LineBreakMode = LineBreakMode.NoWrap;
    }

    private void ArrangeReviewCards()
    {
        var cards = VisibleReviewCards();
        if (!cards.Contains(AdditionalReviewCard))
            Place(AdditionalReviewCard, 0, 0);

        var size = Width > 0 ? ResponsiveBreakpoints.FromWidth(Width) : ScreenSize.Expanded;
        var columns = Math.Min(cards.Count, size switch
        {
            ScreenSize.Compact => 1,
            ScreenSize.Medium => 2,
            _ => 4
        });
        columns = Math.Max(1, columns);
        var rows = Math.Max(1, (int)Math.Ceiling(cards.Count / (double)columns));

        SetColumns(ReviewCardsGrid, columns);
        SetRows(ReviewCardsGrid, rows);
        ReviewCardsGrid.ColumnSpacing = columns == 1 ? 0 : 12;
        ReviewCardsGrid.RowSpacing = 12;
        ReviewCardsGrid.HorizontalOptions = LayoutOptions.Fill;

        for (var i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            card.HeightRequest = -1;
            card.HorizontalOptions = LayoutOptions.Fill;
            card.VerticalOptions = LayoutOptions.Fill;
            Place(card, i % columns, i / columns);
        }

        if (columns > 1)
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(32), EqualizeReviewCardHeights);
    }

    private List<View> VisibleReviewCards()
    {
        var cards = new List<View> { PersonalReviewCard, CompanyReviewCard, PlanReviewCard };
        if (_vm.HasAdditionalReviewDetails)
            cards.Add(AdditionalReviewCard);
        return cards;
    }

    private void EqualizeReviewCardHeights()
    {
        if (_vm.CurrentStep != 4 || Width <= 0)
            return;

        var size = ResponsiveBreakpoints.FromWidth(Width);
        if (size == ScreenSize.Compact)
            return;

        var cards = VisibleReviewCards();
        if (cards.Count < 2)
            return;

        _equalizingReviewHeights = true;
        try
        {
            var byRow = cards.GroupBy(Grid.GetRow);
            foreach (var row in byRow)
            {
                var max = row.Max(card => Math.Max(card.Bounds.Height, card.Height));
                if (max <= 1)
                    continue;

                foreach (var card in row)
                {
                    if (Math.Abs(card.HeightRequest - max) > 0.5)
                        card.HeightRequest = max;
                }
            }
        }
        finally
        {
            _equalizingReviewHeights = false;
        }
    }

    private void ApplyReviewFieldLayout()
    {
        var size = Width > 0 ? ResponsiveBreakpoints.FromWidth(Width) : ScreenSize.Expanded;
        var inline = size != ScreenSize.Compact;
        var columns = VisibleReviewCards().Count;
        if (Width > 0)
        {
            columns = Math.Min(columns, size switch
            {
                ScreenSize.Compact => 1,
                ScreenSize.Medium => 2,
                _ => 4
            });
        }

        var labelWidth = columns >= 3
            ? ResourceDouble("ReviewLabelColumnWidthNarrow", 118)
            : ResourceDouble("ReviewLabelColumnWidth", ReviewFieldRow.DefaultLabelColumnWidth);

        foreach (var row in FindDescendants<ReviewFieldRow>(ReviewCardsGrid))
        {
            row.IsInline = inline;
            row.LabelColumnWidth = labelWidth;
        }
    }

    private static IEnumerable<T> FindDescendants<T>(IView? root) where T : class, IView
    {
        if (root is null)
            yield break;

        if (root is T match)
            yield return match;

        switch (root)
        {
            case ContentView { Content: IView inner }:
                foreach (var child in FindDescendants<T>(inner))
                    yield return child;
                break;
            case Border { Content: IView content }:
                foreach (var child in FindDescendants<T>(content))
                    yield return child;
                break;
            case Microsoft.Maui.Controls.Layout layout:
                foreach (var child in layout.Children.OfType<IView>())
                {
                    foreach (var found in FindDescendants<T>(child))
                        yield return found;
                }
                break;
        }
    }

    private static double ResourceDouble(string key, double fallback)
    {
        if (Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) != true)
            return fallback;

        return value switch
        {
            double d => d,
            float f => f,
            int i => i,
            _ => fallback
        };
    }

    private void ArrangePlanCards()
    {
        var cards = PlanCardsGrid.Children.OfType<View>().ToList();
        if (cards.Count == 0)
        {
            Dispatcher.Dispatch(() =>
            {
                var delayed = PlanCardsGrid.Children.OfType<View>().ToList();
                if (delayed.Count > 0)
                    PlacePlanCards(delayed);
            });
            return;
        }

        PlacePlanCards(cards);
    }

    private void PlacePlanCards(IReadOnlyList<View> cards)
    {
        var width = Width > 0 ? Width : FormHost.Width;
        var columns = ResponsiveBreakpoints.FromWidth(width) switch
        {
            ScreenSize.Compact => 1,
            ScreenSize.Medium => 2,
            _ => 4
        };

        SetColumns(PlanCardsGrid, columns);
        SetRows(PlanCardsGrid, Math.Max(1, (int)Math.Ceiling(cards.Count / (double)columns)));
        PlanCardsGrid.ColumnSpacing = 12;
        PlanCardsGrid.RowSpacing = 12;
        var compact = columns == 1;
        var maxFeatures = 0;
        foreach (var card in cards)
            maxFeatures = Math.Max(maxFeatures, FindPlanCard(card)?.FeatureCount ?? 0);

        _equalizingPlanHeights = true;
        for (var i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            card.HeightRequest = -1;
            card.HorizontalOptions = LayoutOptions.Fill;
            card.VerticalOptions = LayoutOptions.Fill;
            FindPlanCard(card)?.ApplyBenefitsLayout(compact, maxFeatures);
            Place(card, i % columns, i / columns);
        }

        if (!compact)
        {
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(48), () =>
            {
                if (Handler is null)
                {
                    _equalizingPlanHeights = false;
                    return;
                }

                EqualizePlanCardHeights(cards);
            });
            return;
        }

        _equalizingPlanHeights = false;
    }

    private void EqualizePlanCardHeights(IReadOnlyList<View> cards)
    {
        if (_vm.CurrentStep != 3 || Width <= 0 || cards.Count == 0)
        {
            _equalizingPlanHeights = false;
            return;
        }

        if (ResponsiveBreakpoints.FromWidth(Width) == ScreenSize.Compact)
        {
            _equalizingPlanHeights = false;
            return;
        }

        _equalizingPlanHeights = true;
        try
        {
            var max = 0.0;
            foreach (var card in cards)
                max = Math.Max(max, Math.Max(card.Bounds.Height, card.Height));

            if (max <= 1)
                return;

            foreach (var card in cards)
            {
                if (Math.Abs(card.HeightRequest - max) > 1)
                    card.HeightRequest = max;
            }
        }
        finally
        {
            _equalizingPlanHeights = false;
        }
    }

    private static SubscriptionPlanCard? FindPlanCard(View view)
    {
        if (view is SubscriptionPlanCard card)
            return card;

        foreach (var found in FindDescendants<SubscriptionPlanCard>(view))
            return found;

        return null;
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

    private void OnPlanCardTapped(object? sender, TappedEventArgs e)
        => SelectPlanFrom(sender);

    private void OnSelectPlanClicked(object? sender, EventArgs e)
        => SelectPlanFrom(sender);

    private void SelectPlanFrom(object? sender)
    {
        if (sender is BindableObject bindable && bindable.BindingContext is RegisterPlanOption plan)
            _vm.SelectPlanCommand.Execute(plan.Id);
    }

    private bool _actionInFlight;
    private bool _accountCreated;

    private async void OnLoginTapped(object? sender, TappedEventArgs e)
    {
        if (!CanStartAction())
            return;

        await RunActionAsync(async () => await NavigateToLoginScreenAsync());
    }

    private static async Task NavigateToLoginScreenAsync()
    {
        try
        {
            if (Shell.Current.Navigation.NavigationStack.Count > 1)
                await Shell.Current.GoToAsync("..", false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Pop Register failed: {ex.Message}");
        }

        var location = Shell.Current.CurrentState?.Location?.OriginalString ?? string.Empty;
        var onLogin = location.Contains("Login", StringComparison.OrdinalIgnoreCase)
                      && !location.Contains("Register", StringComparison.OrdinalIgnoreCase);
        if (!onLogin)
            await Shell.Current.GoToAsync(AppRoutes.Login, false);
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (!CanStartAction() || !_vm.IsBackVisible)
            return;

        await RunActionAsync(() =>
        {
            _vm.BackCommand.Execute(null);
            return Task.CompletedTask;
        });
    }

    private async void OnPrimaryActionClicked(object? sender, EventArgs e)
    {
        if (!CanStartAction())
            return;

        if (_vm.CurrentStep < 4)
        {
            await RunActionAsync(() =>
            {
                _vm.NextCommand.Execute(null);
                return Task.CompletedTask;
            });
            return;
        }

        await CreateAccountAsync();
    }

    private bool CanStartAction()
        => !_actionInFlight && !_accountCreated && !_vm.IsBusy && !_vm.IsNavigating;

    private async Task RunActionAsync(Func<Task> action)
    {
        _actionInFlight = true;
        _vm.IsNavigating = true;
        SetActionSpinner(true);
        try
        {
            await action();
            await Task.Delay(120);
        }
        finally
        {
            SetActionSpinner(false);
            _vm.IsNavigating = false;
            _actionInFlight = false;
        }
    }

    private void SetActionSpinner(bool running)
    {
        ActionSpinner.IsVisible = running;
        ActionSpinner.IsRunning = running;
    }

    private async Task CreateAccountAsync()
    {
        if (_accountCreated)
            return;

        if (!_vm.Validate())
        {
            _vm.HasGeneralError = true;
            _vm.GeneralError = "Please complete the required information before creating your account.";
            return;
        }

        _actionInFlight = true;
        _vm.IsBusy = true;
        SetActionSpinner(true);
        _vm.HasGeneralError = false;
        _vm.GeneralError = string.Empty;
        _vm.HasSuccessMessage = false;

        try
        {
            var result = await _auth.RegisterAccountAsync(new RegistrationRequest
            {
                FullName = _vm.FullName.Trim(),
                Email = _vm.Email.Trim(),
                Mobile = _vm.Mobile.Trim(),
                Password = _vm.Password,
                CompanyName = _vm.CompanyName.Trim(),
                OwnerName = _vm.OwnerName.Trim(),
                GSTNumber = _vm.GSTNumber.Trim(),
                PANNumber = _vm.PANNumber.Trim(),
                CompanyEmail = _vm.CompanyEmail.Trim(),
                CompanyMobile = string.IsNullOrWhiteSpace(_vm.MobileNumber)
                    ? _vm.Mobile.Trim()
                    : _vm.MobileNumber.Trim(),
                Country = _vm.Country.Trim(),
                State = _vm.State.Trim(),
                City = _vm.City.Trim(),
                PinCode = _vm.PinCode.Trim(),
                Address = _vm.Address.Trim(),
                PlanId = _vm.SelectedPlanId,
                PlanName = _vm.SelectedPlanName
            });

            if (!result.Success || !result.LocalAccountCreated)
            {
                _vm.HasGeneralError = true;
                _vm.GeneralError = result.Error ?? "Account could not be created. Please try again.";
                return;
            }

            _accountCreated = true;
            _toasts.ShowSuccess(
                "Account Completed Successfully",
                "Local account created successfully.\nServer sync is pending and will complete when internet is available.",
                TimeSpan.FromSeconds(2));
            await Task.Delay(TimeSpan.FromSeconds(2));
            await _auth.LogoutAsync();
            await NavigateToLoginScreenAsync();
            _vm.Reset();
        }
        catch (Exception ex)
        {
            _vm.HasGeneralError = true;
            _vm.GeneralError = $"Account could not be created. {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[AveroNova] CreateAccount failed: {ex}");
        }
        finally
        {
            if (!_accountCreated)
            {
                _vm.IsBusy = false;
                SetActionSpinner(false);
                _actionInFlight = false;
            }
        }
    }
}
