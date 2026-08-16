using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Maui.Controls.Shapes;

namespace AveroNova.App.UI.Pages.Expenses;

[QueryProperty(nameof(ExpenseId), "id")]
public partial class ExpenseViewPage : ContentPage
{
    private readonly IExpenseService _svc;
    private ExpenseModel? _expense;
    public string? ExpenseId { get; set; }

    public ExpenseViewPage(IExpenseService svc) { InitializeComponent(); _svc = svc; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(ExpenseId) && Guid.TryParse(ExpenseId, out var id))
        {
            _expense = await _svc.GetByIdAsync(id);
            if (_expense != null) BuildContent(_expense);
        }
    }

    private void BuildContent(ExpenseModel exp)
    {
        Content.Children.Clear();

        var headerCard = new Border { Style = (Style)Resources["AppCard"] };
        var (bg, color) = exp.Status switch
        {
            ExpenseStatus.Approved => ("#EFF6FF", "#2563EB"),
            ExpenseStatus.Paid     => ("#ECFDF5", "#059669"),
            ExpenseStatus.Rejected => ("#FEF2F2", "#DC2626"),
            _                      => ("#FFFBEB", "#D97706")
        };
        var hGrid = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 16 };
        var hLeft = new VerticalStackLayout { Spacing = 4 };
        hLeft.Children.Add(new Label { Text = exp.Category, FontSize = 20, FontAttributes = FontAttributes.Bold });
        hLeft.Children.Add(new Label { Text = exp.Description, FontSize = 13, TextColor = Color.FromArgb("#64748B") });
        hLeft.Children.Add(new Label { Text = $"{exp.ExpenseDate:dd MMM yyyy}  •  {exp.Method}", FontSize = 12, TextColor = Color.FromArgb("#94A3B8") });
        var hRight = new VerticalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.End };
        hRight.Children.Add(new Label { Text = $"${exp.Amount:N2}", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#DC2626"), HorizontalOptions = LayoutOptions.End });
        var badge = new Border { BackgroundColor = Color.FromArgb(bg), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) }, Padding = new Thickness(10, 4), HorizontalOptions = LayoutOptions.End };
        badge.Content = new Label { Text = exp.StatusLabel, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(color) };
        hRight.Children.Add(badge);
        hGrid.Add(hLeft, 0, 0);
        hGrid.Add(hRight, 1, 0);
        headerCard.Content = hGrid;

        var detailCard = new Border { Style = (Style)Resources["AppCard"] };
        var dVsl = new VerticalStackLayout { Spacing = 12 };
        dVsl.Children.Add(new Label { Text = "Details", FontSize = 14, FontAttributes = FontAttributes.Bold });
        dVsl.Children.Add(new BoxView { Style = (Style)Resources["Divider"] });
        void AddDetail(string label, string value)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(new GridLength(130)), new ColumnDefinition(GridLength.Star)) };
            g.Add(new Label { Text = label, FontSize = 13, TextColor = Color.FromArgb("#64748B") }, 0, 0);
            g.Add(new Label { Text = value, FontSize = 13, FontAttributes = FontAttributes.Bold }, 1, 0);
            dVsl.Children.Add(g);
        }
        AddDetail("Category", exp.Category);
        AddDetail("Amount", $"${exp.Amount:N2}");
        AddDetail("Date", exp.ExpenseDate.ToString("dd MMM yyyy"));
        AddDetail("Method", exp.Method.ToString());
        AddDetail("Status", exp.StatusLabel);
        if (!string.IsNullOrEmpty(exp.Description)) AddDetail("Description", exp.Description);
        if (!string.IsNullOrEmpty(exp.Notes)) AddDetail("Notes", exp.Notes);
        detailCard.Content = dVsl;

        Content.Children.Add(headerCard);
        Content.Children.Add(detailCard);
    }

    private async void OnEditClicked(object s, EventArgs e) => await Shell.Current.GoToAsync($"{AppRoutes.ExpenseEdit}?id={_expense?.LocalId}");
    private async void OnBackClicked(object s, EventArgs e) => await Shell.Current.GoToAsync("..");

    private async void OnDeleteClicked(object s, EventArgs e)
    {
        if (_expense == null) return;
        if (!await DialogHelper.ConfirmDeleteAsync("Expense", $"Delete {_expense.Category} expense?")) return;
        await _svc.DeleteAsync(_expense.LocalId);
        await Shell.Current.GoToAsync("..");
    }
}
