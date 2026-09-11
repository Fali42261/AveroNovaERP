using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Purchases;

public partial class SuppliersListPage : ContentPage, IHostedPage
{
    private readonly ISupplierService _service;
    private readonly ICompanyService _company;
    private readonly IMainContentNavigator _nav;
    private readonly Func<SupplierFormPage> _formFactory;
    private bool _loading;

    public SuppliersListPage(
        ISupplierService service,
        ICompanyService company,
        IMainContentNavigator nav,
        Func<SupplierFormPage> formFactory)
    {
        InitializeComponent();
        _service = service;
        _company = company;
        _nav = nav;
        _formFactory = formFactory;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadForHostAsync();
    }

    public async Task LoadForHostAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            var companyId = _company.CurrentCompany?.LocalId ?? Guid.Empty;
            var rows = companyId == Guid.Empty ? [] : await _service.GetAllAsync(companyId);
            LblCount.Text = $"{rows.Count} supplier{(rows.Count == 1 ? string.Empty : "s")}";
            List.Children.Clear();
            foreach (var x in rows.OrderBy(x => x.Name))
                List.Children.Add(BuildRow(x));

            if (rows.Count == 0)
            {
                List.Children.Add(new Label
                {
                    Text = "No suppliers found. Create a supplier before adding a purchase.",
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 40),
                    TextColor = Color.FromArgb("#64748B")
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Suppliers] Load failed: {ex}");
            List.Children.Clear();
            List.Children.Add(new Label
            {
                Text = "Suppliers could not be loaded. Pull down to retry.",
                TextColor = Color.FromArgb("#DC2626"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 40)
            });
        }
        finally
        {
            _loading = false;
        }
    }

    private View BuildRow(SupplierModel x)
    {
        var border = new Border
        {
            Stroke = Color.FromArgb("#E2E8F0"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Padding = new Thickness(14, 12),
            BackgroundColor = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#1E293B") : Colors.White
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = 10
        };
        var info = new VerticalStackLayout { Spacing = 3 };
        info.Children.Add(new Label { Text = x.Name, FontAttributes = FontAttributes.Bold, FontSize = 15 });
        info.Children.Add(new Label
        {
            Text = string.Join(" • ", new[] { x.Phone, x.Email }.Where(s => !string.IsNullOrWhiteSpace(s))),
            FontSize = 12,
            TextColor = Color.FromArgb("#64748B")
        });

        var edit = MakeActionButton("Edit");
        edit.Clicked += async (_, _) =>
        {
            try
            {
                var page = _formFactory();
                page.EditId = x.LocalId;
                await _nav.NavigateAsync(page, "Edit Supplier", "Home / Purchases / Suppliers / Edit");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Suppliers] Edit failed: {ex}");
                await DisplayAlert("Suppliers", "Supplier could not be opened.", "OK");
            }
        };

        grid.Add(info, 0, 0);
        grid.Add(edit, 1, 0);
        border.Content = grid;
        return border;
    }

    private static Button MakeActionButton(string text) => new()
    {
        Text = text,
        FontSize = 12,
        HeightRequest = 36,
        Padding = new Thickness(12, 0),
        CornerRadius = 8,
        BackgroundColor = Colors.Transparent,
        TextColor = Color.FromArgb("#2563EB"),
        BorderColor = Color.FromArgb("#2563EB"),
        BorderWidth = 1
    };

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        try { await LoadForHostAsync(); }
        finally { Refresher.IsRefreshing = false; }
    }

    private async void OnNewClicked(object? sender, EventArgs e)
    {
        try { await _nav.NavigateAsync(_formFactory(), "New Supplier", "Home / Purchases / Suppliers / New"); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Suppliers] New failed: {ex}");
            await DisplayAlert("Suppliers", "Supplier form could not be opened.", "OK");
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        try { await _nav.GoBackAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Suppliers] Back failed: {ex}"); }
    }
}
