using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Pages.Expenses;

[QueryProperty(nameof(EditId), "id")]
public partial class ExpenseFormPage : ContentPage
{
    private readonly IExpenseService _svc;
    private readonly ICompanyService _company;
    private ExpenseModel? _editing;
    public string? EditId { get; set; }

    public ExpenseFormPage(IExpenseService svc, ICompanyService company) { InitializeComponent(); _svc = svc; _company = company; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrEmpty(EditId) && Guid.TryParse(EditId, out var id))
        {
            _editing = await _svc.GetByIdAsync(id);
            if (_editing != null)
            {
                LblTitle.Text = "Edit Expense";
                EntryCategory.Text = _editing.Category;
                EntryAmount.Text = _editing.Amount.ToString("N2");
                DateExpense.Date = _editing.ExpenseDate;
                //EntryMethod.Text = _editing.Method;
                EditorDescription.Text = _editing.Description;
                EditorNotes.Text = _editing.Notes;
            }
        }
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryCategory.Text)) { ShowError("Category is required."); return; }
        if (!decimal.TryParse(EntryAmount.Text, out var amount)) { ShowError("Valid amount is required."); return; }
        var model = _editing ?? new ExpenseModel { CompanyId = _company.CurrentCompany?.LocalId ?? Guid.Empty };
        model.Category = EntryCategory.Text.Trim();
        model.Amount = amount;
        //model.ExpenseDate = DateExpense.Date;
        //model.Method = EntryMethod.Text?.Trim() ?? "";
        model.Description = EditorDescription.Text?.Trim() ?? "";
        model.Notes = EditorNotes.Text?.Trim() ?? "";
        var (ok, err) = _editing == null ? await _svc.CreateAsync(model) : await _svc.UpdateAsync(model);
        if (ok) await Shell.Current.GoToAsync("..");
        else ShowError(err ?? "Save failed.");
    }

    private async void OnBackClicked(object s, EventArgs e) => await Shell.Current.GoToAsync("..");
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
