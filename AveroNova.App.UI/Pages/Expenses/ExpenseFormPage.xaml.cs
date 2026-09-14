using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Navigation;

namespace AveroNova.App.UI.Pages.Expenses;

[QueryProperty(nameof(EditId), "id")]
public partial class ExpenseFormPage : ContentPage, IHostedPage
{
    private readonly IExpenseService _svc;
    private readonly ICompanyService _company;
    private readonly ISettingsService _settings;
    private readonly ISyncService _sync;
    private readonly IConnectivityService _connectivity;
    private readonly IMainContentNavigator _navigator;
    private ExpenseModel? _editing;
    private bool _loading;
    private bool _saving;
    public string? EditId { get; set; }

    public ExpenseFormPage(
        IExpenseService svc,
        ICompanyService company,
        ISettingsService settings,
        ISyncService sync,
        IConnectivityService connectivity,
        IMainContentNavigator navigator)
    {
        InitializeComponent();
        _svc = svc;
        _company = company;
        _settings = settings;
        _sync = sync;
        _connectivity = connectivity;
        _navigator = navigator;
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
            ErrorBanner.IsVisible = false;
            var companyId = _company.CurrentCompany?.LocalId ?? Guid.Empty;
            if (companyId == Guid.Empty)
            {
                ShowError("Select a company before adding an expense.");
                return;
            }

            var categories = await _svc.GetCategoriesAsync(companyId);
            CategoryPicker.ItemsSource = categories;
            if (_editing is null)
            {
                DateExpense.Date = DateTime.Today;
                MethodPicker.SelectedIndex = 0;
                StatusPicker.SelectedIndex = 0;
            }
            if (!string.IsNullOrEmpty(EditId) && _editing is null && Guid.TryParse(EditId, out var id))
            {
                _editing = await _svc.GetByIdAsync(id);
                if (_editing is null)
                {
                    ShowError("Expense could not be found.");
                    return;
                }

                LblTitle.Text = "Edit Expense";
                CategoryPicker.SelectedIndex = categories.FindIndex(x => x == _editing.Category);
                EntryAmount.Text = _editing.Amount.ToString("0.00");
                DateExpense.Date = _editing.ExpenseDate;
                MethodPicker.SelectedIndex = (int)_editing.Method;
                StatusPicker.SelectedIndex = (int)_editing.Status;
                EntryReference.Text = _editing.Reference;
                EntryApprovedBy.Text = _editing.ApprovedBy;
                EditorDescription.Text = _editing.Description;
                EditorNotes.Text = _editing.Notes;
                OnStatusChanged(null, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExpenseForm] Load failed: {ex}");
            ShowError("Expense form could not be loaded.");
        }
        finally
        {
            _loading = false;
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_saving) return;
        ErrorBanner.IsVisible = false;
        if (CategoryPicker.SelectedIndex < 0) { ShowError("Select a category."); return; }
        if (!decimal.TryParse(EntryAmount.Text, out var amount) || amount <= 0) { ShowError("Amount must be greater than zero."); return; }

        _saving = true;
        try
        {
            var model = _editing ?? new ExpenseModel { CompanyId = _company.CurrentCompany?.LocalId ?? Guid.Empty };
            model.Category = CategoryPicker.SelectedItem?.ToString() ?? string.Empty;
            model.Amount = amount;
            model.ExpenseDate = DateExpense.Date ?? DateTime.Today;
            model.Method = (PaymentMethod)Math.Max(0, MethodPicker.SelectedIndex);
            model.Status = (ExpenseStatus)Math.Max(0, StatusPicker.SelectedIndex);
            model.Reference = EntryReference.Text?.Trim() ?? string.Empty;
            model.ApprovedBy = EntryApprovedBy.Text?.Trim() ?? string.Empty;
            model.Description = EditorDescription.Text?.Trim() ?? string.Empty;
            model.Notes = EditorNotes.Text?.Trim() ?? string.Empty;

            var (ok, error) = _editing is null
                ? await _svc.CreateAsync(model)
                : await _svc.UpdateAsync(model);
            if (!ok)
            {
                ShowError(error ?? "Expense could not be saved.");
                return;
            }

            if (_connectivity.IsOnline && (await _settings.GetAsync()).AutoSync)
            {
                var synced = await _sync.SyncNowAsync();
                if (!synced)
                    await DisplayAlert("Saved locally", "The expense is safe on this device and will retry syncing automatically.", "OK");
            }

            await _navigator.GoBackAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExpenseForm] Save failed: {ex}");
            ShowError("Expense could not be saved. Please try again.");
        }
        finally
        {
            _saving = false;
        }
    }

    private void OnStatusChanged(object? s,EventArgs e)=>ApprovedBySection.IsVisible=StatusPicker.SelectedIndex is 1 or 3;
    private async void OnBackClicked(object? sender, EventArgs e)
    {
        try { await _navigator.GoBackAsync(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ExpenseForm] Back failed: {ex}"); }
    }
    private void ShowError(string msg) { LblError.Text = msg; ErrorBanner.IsVisible = true; }
}
