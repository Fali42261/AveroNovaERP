using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Layout;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.ViewModels;

namespace AveroNova.App.UI.Pages.Authentication;

public partial class WelcomePage : ContentPage
{
    private readonly IResponsiveLayoutService _layout;
    private readonly RegistrationWizardViewModel _wizard;
    private bool _createCompanySelected;

    public WelcomePage(IResponsiveLayoutService layout, RegistrationWizardViewModel wizard)
    {
        InitializeComponent();
        _layout = layout;
        _wizard = wizard;
        _layout.LayoutChanged += OnLayoutChanged;
        SizeChanged += (_, _) => ApplyLayout();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLayout();
        ApplySelection();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler is null)
            _layout.LayoutChanged -= OnLayoutChanged;
    }

    private void OnLayoutChanged(object? sender, EventArgs e) => ApplyLayout();

    private void ApplyLayout()
    {
        var expanded = _layout.UseTwoPane || Width >= ResponsiveBreakpoints.ExpandedMinWidth;
        ExpandedLayout.IsVisible = expanded;
        CompactLayout.IsVisible = !expanded;
    }

    private void OnCreateCompanyTapped(object? sender, TappedEventArgs e)
    {
        _createCompanySelected = true;
        ApplySelection();
    }

    private void ApplySelection()
    {
        var selected = GetStyle("SelectableCardSelected");
        var normal = GetStyle("SelectableCard");

        if (CompanyCardExpanded != null)
            CompanyCardExpanded.Style = _createCompanySelected ? selected ?? normal : normal;
        if (CompanyCardCompact != null)
            CompanyCardCompact.Style = _createCompanySelected ? selected ?? normal : normal;

        BtnContinueExpanded.IsEnabled = _createCompanySelected;
        BtnContinueCompact.IsEnabled = _createCompanySelected;
    }

    private static Style? GetStyle(string key)
        => Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true
           ? value as Style
           : null;

    private async void OnContinueClicked(object? sender, EventArgs e)
    {
        if (!_createCompanySelected || sender is not Button button)
            return;

        await BusyButton.RunAsync(button, async () =>
        {
            _wizard.BeginNewCompanyRegistration();
            await Shell.Current.GoToAsync(AppRoutes.Register);
        });
    }

    private async void OnLoginClicked(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(AppRoutes.Login);
}
