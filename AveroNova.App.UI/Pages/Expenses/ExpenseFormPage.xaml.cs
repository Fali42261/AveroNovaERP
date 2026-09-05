using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Navigation;

namespace AveroNova.App.UI.Pages.Expenses;

[QueryProperty(nameof(EditId), "id")]
public partial class ExpenseFormPage : ContentPage, IHostedPage
{
    private readonly IExpenseService _svc;
    private readonly ICompanyService _company;
    private readonly IMainContentNavigator _navigator;
    private ExpenseModel? _editing;
    public string? EditId { get; set; }

    public ExpenseFormPage(IExpenseService svc, ICompanyService company, IMainContentNavigator navigator) { InitializeComponent(); _svc = svc; _company = company; _navigator=navigator; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadForHostAsync();
    }

    public async Task LoadForHostAsync()
    {
        ErrorBanner.IsVisible=false;
        var categories=await _svc.GetCategoriesAsync(_company.CurrentCompany?.LocalId??Guid.Empty);
        CategoryPicker.ItemsSource=categories;
        if (_editing is null) { DateExpense.Date=DateTime.Today; MethodPicker.SelectedIndex=0; StatusPicker.SelectedIndex=0; }
        if (!string.IsNullOrEmpty(EditId) && _editing is null && Guid.TryParse(EditId, out var id))
        {
            _editing = await _svc.GetByIdAsync(id);
            if (_editing != null)
            {
                LblTitle.Text = "Edit Expense";
                CategoryPicker.SelectedIndex = categories.FindIndex(x=>x==_editing.Category);
                EntryAmount.Text = _editing.Amount.ToString("N2");
                DateExpense.Date = _editing.ExpenseDate;
                MethodPicker.SelectedIndex = (int)_editing.Method;
                StatusPicker.SelectedIndex = (int)_editing.Status;
                EntryReference.Text = _editing.Reference;
                EntryApprovedBy.Text = _editing.ApprovedBy;
                EditorDescription.Text = _editing.Description;
                EditorNotes.Text = _editing.Notes;
            }
        }
    }

    private async void OnSaveClicked(object s, EventArgs e)
    {
        if (CategoryPicker.SelectedIndex<0) { ShowError("Select a category."); return; }
        if (!decimal.TryParse(EntryAmount.Text, out var amount) || amount<=0) { ShowError("Amount must be greater than zero."); return; }
        var model = _editing ?? new ExpenseModel { CompanyId = _company.CurrentCompany?.LocalId ?? Guid.Empty };
        model.Category = CategoryPicker.SelectedItem?.ToString()??"";
        model.Amount = amount;
        model.ExpenseDate = DateExpense.Date??DateTime.Today;
        model.Method = (PaymentMethod)Math.Max(0,MethodPicker.SelectedIndex);
        model.Status = (ExpenseStatus)Math.Max(0,StatusPicker.SelectedIndex);
        model.Reference = EntryReference.Text?.Trim()??"";
        model.ApprovedBy = EntryApprovedBy.Text?.Trim()??"";
        model.Description = EditorDescription.Text?.Trim() ?? "";
        model.Notes = EditorNotes.Text?.Trim() ?? "";
        var (ok, err) = _editing == null ? await _svc.CreateAsync(model) : await _svc.UpdateAsync(model);
        if (ok) await _navigator.GoBackAsync();
        else ShowError(err ?? "Save failed.");
    }

    private void OnStatusChanged(object? s,EventArgs e)=>ApprovedBySection.IsVisible=StatusPicker.SelectedIndex is 1 or 3;
    private async void OnBackClicked(object s, EventArgs e) => await _navigator.GoBackAsync();
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
