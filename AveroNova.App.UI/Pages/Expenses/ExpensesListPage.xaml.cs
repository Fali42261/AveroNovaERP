using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Expenses;

public partial class ExpensesListPage : ContentPage, IHostedPage
{
    private readonly IExpenseService _svc;
    private readonly ICompanyService _company;
    private readonly IMainContentNavigator _navigator;
    private readonly Func<ExpenseFormPage> _formFactory;
    private readonly Func<ExpenseViewPage> _viewFactory;

    public ExpensesListPage(IExpenseService svc, ICompanyService company, IMainContentNavigator navigator, Func<ExpenseFormPage> formFactory, Func<ExpenseViewPage> viewFactory)
    { InitializeComponent(); _svc = svc; _company = company; _navigator=navigator; _formFactory=formFactory; _viewFactory=viewFactory; }

    protected override async void OnAppearing()    { base.OnAppearing(); await LoadAsync(); }
    public Task LoadForHostAsync()=>LoadAsync();
    private async void OnRefreshing(object s, EventArgs e) { await LoadAsync(); Refresher.IsRefreshing = false; }

    private async Task LoadAsync()
    {
        var items = await _svc.GetAllAsync(_company.CurrentCompany?.LocalId ?? Guid.Empty);
        LblTotal.Text = $"Total: ${items.Sum(e => e.Amount):N2}";
        List.Children.Clear();
        foreach (var e in items.OrderByDescending(x => x.ExpenseDate)) List.Children.Add(BuildRow(e));
        if (items.Count == 0) List.Children.Add(new Label { Text = "No expenses found.", FontSize = 14, TextColor = Color.FromArgb("#64748B"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 40) });
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
        left.Children.Add(new Label { Text = $"{exp.ExpenseDate:dd MMM yyyy}  •  {exp.Method}", FontSize = 11, TextColor = Color.FromArgb("#94A3B8") });

        var right = new VerticalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.End };
        right.Children.Add(new Label { Text = $"${exp.Amount:N2}", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#DC2626") });
        var badge = new Border { BackgroundColor = Color.FromArgb(bg), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) }, Padding = new Thickness(8, 3) };
        badge.Content = new Label { Text = exp.StatusLabel, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(color) };
        right.Children.Add(badge);
        var viewBtn = new Button { Text = "View", Style = (Style)Resources["SmallSecondaryButton"] };
        viewBtn.Clicked += async (_, _) => {var page=_viewFactory();page.ExpenseId=exp.LocalId.ToString("D");await _navigator.NavigateAsync(page,"Expense Details","Home / Expenses / Details");};
        right.Children.Add(viewBtn);

        grid.Add(left,  0, 0);
        grid.Add(right, 1, 0);
        border.Content = grid;
        return border;
    }

    private async void OnAddClicked(object s, EventArgs e) => await _navigator.NavigateAsync(_formFactory(),"Add Expense","Home / Expenses / Add");
}
