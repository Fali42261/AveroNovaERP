using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Expenses;

public partial class ExpensesListPage : ContentPage, IHostedPage
{
    private readonly IExpenseService _svc;
    private readonly ICompanyService _company;
    private readonly ISettingsService _settings;
    private readonly ISyncService _sync;
    private readonly IConnectivityService _connectivity;
    private readonly IMainContentNavigator _navigator;
    private readonly Func<ExpenseFormPage> _formFactory;
    private readonly Func<ExpenseViewPage> _viewFactory;
    private string _currencySymbol = "₹";
    private string _dateFormat = "dd MMM yyyy";
    private bool _loading;

    public ExpensesListPage(
        IExpenseService svc,
        ICompanyService company,
        ISettingsService settings,
        ISyncService sync,
        IConnectivityService connectivity,
        IMainContentNavigator navigator,
        Func<ExpenseFormPage> formFactory,
        Func<ExpenseViewPage> viewFactory)
    {
        InitializeComponent();
        _svc = svc;
        _company = company;
        _settings = settings;
        _sync = sync;
        _connectivity = connectivity;
        _navigator = navigator;
        _formFactory = formFactory;
        _viewFactory = viewFactory;
    }

    protected override async void OnAppearing() { base.OnAppearing(); await LoadAsync(); }
    public Task LoadForHostAsync() => LoadAsync();

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        try
        {
            if (_connectivity.IsOnline)
                await _sync.SyncNowAsync();
            await LoadAsync();
        }
        finally
        {
            Refresher.IsRefreshing = false;
        }
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            var appSettings = await _settings.GetAsync();
            _currencySymbol = string.IsNullOrWhiteSpace(appSettings.CurrencySymbol)
                ? (appSettings.Currency == "INR" ? "₹" : "$")
                : appSettings.CurrencySymbol;
            _dateFormat = string.IsNullOrWhiteSpace(appSettings.DateFormat) ? "dd MMM yyyy" : appSettings.DateFormat;

            var items = await _svc.GetAllAsync(_company.CurrentCompany?.LocalId ?? Guid.Empty);
            LblTotal.Text = $"Total: {Money(items.Sum(expense => expense.Amount))}";
            List.Children.Clear();
            foreach (var expense in items.OrderByDescending(x => x.ExpenseDate))
                List.Children.Add(BuildRow(expense));
            if (items.Count == 0)
                List.Children.Add(new Label { Text = "No expenses found.", FontSize = 14, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 40) });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Expenses] Load failed: {ex}");
            List.Children.Clear();
            List.Children.Add(new Label { Text = "Expenses could not be loaded. Pull down to retry.", TextColor = Color.FromArgb("#DC2626"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 40) });
        }
        finally
        {
            _loading = false;
        }
    }

    private View BuildRow(ExpenseModel exp)
    {
        var (bg, color) = exp.Status switch
        {
            ExpenseStatus.Approved => ("#EFF6FF", "#2563EB"),
            ExpenseStatus.Paid     => ("#ECFDF5", "#059669"),
            ExpenseStatus.Rejected => ("#FEF2F2", "#DC2626"),
            _                      => ("#FFFBEB", "#D97706")
        };
        var border = new Border { BackgroundColor = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1E293B") : Colors.White, Stroke = Color.FromArgb("#E2E8F0"), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) }, Padding = new Thickness(14, 12) };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12 };

        var left = new VerticalStackLayout { Spacing = 3 };
        left.Children.Add(new Label { Text = exp.Category, FontSize = 14, FontAttributes = FontAttributes.Bold });
        left.Children.Add(new Label { Text = exp.Description, FontSize = 13, TextColor = Color.FromArgb("#64748B") });
        left.Children.Add(new Label { Text = $"{exp.ExpenseDate.ToString(_dateFormat)}  •  {exp.Method}", FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });

        var right = new VerticalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label { Text = Money(exp.Amount), FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#DC2626") });
        var badge = new Border { BackgroundColor = Color.FromArgb(bg), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) }, Padding = new Thickness(8, 3) };
        badge.Content = new Label { Text = exp.StatusLabel, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(color) };
        right.Children.Add(badge);
        var syncText = exp.SyncStatus switch
        {
            SyncStatus.Synced => "Synced",
            SyncStatus.SyncFailed => "Sync failed",
            _ => "Pending sync"
        };
        right.Children.Add(new Label
        {
            Text = syncText,
            FontSize = 10,
            TextColor = exp.SyncStatus == SyncStatus.Synced ? Color.FromArgb("#059669")
                : exp.SyncStatus == SyncStatus.SyncFailed ? Color.FromArgb("#DC2626")
                : Color.FromArgb("#D97706"),
            HorizontalOptions = LayoutOptions.End
        });
        var viewBtn = new Button
        {
            Text = "View",
            FontSize = 12,
            HeightRequest = 36,
            Padding = new Thickness(12, 0),
            CornerRadius = 8,
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#2563EB"),
            BorderColor = Color.FromArgb("#2563EB"),
            BorderWidth = 1
        };
        viewBtn.Clicked += async (_, _) => {var page=_viewFactory();page.ExpenseId=exp.LocalId.ToString("D");await _navigator.NavigateAsync(page,"Expense Details","Home / Expenses / Details");};
        right.Children.Add(viewBtn);

        grid.Add(left,  0, 0);
        grid.Add(right, 1, 0);
        border.Content = grid;
        return border;
    }

    private string Money(decimal amount) => $"{_currencySymbol}{amount:N2}";

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        try { await _navigator.NavigateAsync(_formFactory(), "Add Expense", "Home / Expenses / Add"); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Expenses] Add navigation failed: {ex}");
            await DisplayAlert("Expenses", "Expense form could not be opened.", "OK");
        }
    }
}
