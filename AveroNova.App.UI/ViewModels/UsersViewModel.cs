using System.Collections.ObjectModel;
using System.Diagnostics;
using AveroNova.App.UI.Helpers;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.Local;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Domain.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AveroNova.App.UI.ViewModels;

public sealed class RoleFilterOption
{
    public Guid? RoleId { get; init; }
    public string Name { get; init; } = "All";
    public override string ToString() => Name;
}

public partial class UsersViewModel : ObservableObject
{
    public const string StatusFilterAll = "All";

    private readonly IUserService _users;
    private readonly CurrentAccessService _access;
    private readonly IToastService _toasts;
    private int _loadSerial;
    private CancellationTokenSource? _searchCts;
    private bool _suppressFilterReload;

    public UsersViewModel(IUserService users, CurrentAccessService access, IToastService toasts)
    {
        _users = users;
        _access = access;
        _toasts = toasts;
        UserChangeNotifier.Succeeded += OnUserChanged;
    }

    public ObservableCollection<UserModel> Items { get; } = [];
    public ObservableCollection<RoleFilterOption> RoleFilters { get; } = [];

    public IReadOnlyList<string> StatusFilters { get; } = [StatusFilterAll, "Active", "Inactive"];

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string selectedStatusFilter = StatusFilterAll;
    [ObservableProperty] private RoleFilterOption? selectedRoleFilter;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isDeleting;
    [ObservableProperty] private bool hasLoadError;
    [ObservableProperty] private bool isCompact;
    [ObservableProperty] private bool canView;
    [ObservableProperty] private bool canCreate;
    [ObservableProperty] private bool canUpdate;
    [ObservableProperty] private bool canDelete;
    [ObservableProperty] private bool canAssignRole;
    [ObservableProperty] private int totalCount;
    [ObservableProperty] private string countLabel = "0 users";

    public bool ShowLoading => IsLoading;
    public bool ShowError => HasLoadError && !IsLoading;
    public bool ShowEmpty => !IsLoading && !HasLoadError && Items.Count == 0;
    public bool ShowList => !IsLoading && !HasLoadError && Items.Count > 0;
    public bool ShowAddButton => CanCreate && !IsLoading;
    public string? EmptyActionLabel => CanCreate ? "+ Add User" : null;
    public bool ShowDesktopTable => ShowList && !IsCompact;
    public bool ShowMobileCards => ShowList && IsCompact;
    public bool CanRunDelete => CanDelete && !IsDeleting && !IsLoading;

    public event EventHandler? AddRequested;
    public event EventHandler<UserModel>? ViewRequested;
    public event EventHandler<UserModel>? EditRequested;

    public async Task LoadAsync(bool showLoading = true)
    {
        var serial = ++_loadSerial;
        if (showLoading)
            IsLoading = true;
        HasLoadError = false;
        NotifyUiState();

        try
        {
            await RefreshPermissionsAsync();
            await LoadRoleFiltersAsync();

            var users = await _users.QueryAsync(new UserListQuery
            {
                SearchText = SearchText,
                RoleId = SelectedRoleFilter?.RoleId,
                Status = ParseStatusFilter(SelectedStatusFilter)
            });

            if (serial != _loadSerial)
                return;

            Items.Clear();
            foreach (var user in users)
                Items.Add(user);

            TotalCount = users.Count;
            CountLabel = $"{users.Count} user{(users.Count == 1 ? string.Empty : "s")}";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AveroNova] Users load failed: {ex.Message}");
            if (serial == _loadSerial)
            {
                HasLoadError = true;
                Items.Clear();
            }
        }
        finally
        {
            if (serial == _loadSerial)
            {
                IsLoading = false;
                NotifyUiState();
            }
        }
    }

    [RelayCommand]
    private void Add()
    {
        if (!CanCreate)
            return;
        AddRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void View(UserModel? user)
    {
        if (user == null || !CanView)
            return;
        ViewRequested?.Invoke(this, user);
    }

    [RelayCommand]
    private void Edit(UserModel? user)
    {
        if (user == null || !CanUpdate || user.IsOwner)
            return;
        EditRequested?.Invoke(this, user);
    }

    [RelayCommand]
    private async Task DeleteAsync(UserModel? user)
    {
        if (user == null || !CanRunDelete || user.IsOwner)
            return;

        var confirmed = await DialogHelper.ConfirmDeleteAsync(
            "User",
            "Are you sure you want to delete this user?");
        if (!confirmed)
            return;

        IsDeleting = true;
        NotifyUiState();
        try
        {
            var (ok, error) = await _users.DeleteAsync(user.LocalId);
            if (!ok)
            {
                _toasts.ShowError("Unable to delete user.", error ?? "Please try again.");
                return;
            }

            _toasts.ShowSuccess("User deleted successfully.", string.Empty);
            await LoadAsync(showLoading: false);
        }
        finally
        {
            IsDeleting = false;
            NotifyUiState();
        }
    }

    public bool CanEditUser(UserModel user) => CanUpdate && !user.IsOwner;
    public bool CanDeleteUser(UserModel user) => CanDelete && !user.IsOwner && !IsDeleting;

    private void OnUserChanged(object? sender, string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!string.IsNullOrWhiteSpace(message))
                _toasts.ShowSuccess(message, string.Empty);
            _ = LoadAsync(showLoading: false);
        });
    }

    partial void OnSearchTextChanged(string value) => QueueReload();
    partial void OnSelectedStatusFilterChanged(string value)
    {
        if (!_suppressFilterReload)
            _ = LoadAsync(showLoading: false);
    }

    partial void OnSelectedRoleFilterChanged(RoleFilterOption? value)
    {
        if (!_suppressFilterReload)
            _ = LoadAsync(showLoading: false);
    }
    partial void OnIsCompactChanged(bool value) => NotifyUiState();

    private async Task RefreshPermissionsAsync()
    {
        var snapshot = await _access.GetSnapshotAsync();
        CanView = PermissionNames.Grants(snapshot.Permissions, PermissionNames.UsersView);
        CanCreate = PermissionNames.Grants(snapshot.Permissions, PermissionNames.UsersCreate);
        CanUpdate = PermissionNames.Grants(snapshot.Permissions, PermissionNames.UsersUpdate);
        CanDelete = PermissionNames.Grants(snapshot.Permissions, PermissionNames.UsersDelete);
        CanAssignRole = PermissionNames.Grants(snapshot.Permissions, PermissionNames.UsersAssignRole);
    }

    private async Task LoadRoleFiltersAsync()
    {
        _suppressFilterReload = true;
        try
        {
            var selectedId = SelectedRoleFilter?.RoleId;
            var roles = await _users.GetAssignableRolesAsync();
            RoleFilters.Clear();
            RoleFilters.Add(new RoleFilterOption { Name = "All" });
            foreach (var role in roles)
                RoleFilters.Add(new RoleFilterOption { RoleId = role.LocalId, Name = role.Name });

            SelectedRoleFilter = RoleFilters.FirstOrDefault(r => r.RoleId == selectedId) ?? RoleFilters[0];
        }
        finally
        {
            _suppressFilterReload = false;
        }
    }

    private void QueueReload()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        _ = ReloadAfterDelayAsync(token);
    }

    private async Task ReloadAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(280, token);
            await LoadAsync(showLoading: false);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private static UserStatus? ParseStatusFilter(string filter)
        => filter switch
        {
            "Active" => UserStatus.Active,
            "Inactive" => UserStatus.Inactive,
            _ => null
        };

    private void NotifyUiState()
    {
        OnPropertyChanged(nameof(ShowLoading));
        OnPropertyChanged(nameof(ShowError));
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowList));
        OnPropertyChanged(nameof(ShowAddButton));
        OnPropertyChanged(nameof(EmptyActionLabel));
        OnPropertyChanged(nameof(ShowDesktopTable));
        OnPropertyChanged(nameof(ShowMobileCards));
        OnPropertyChanged(nameof(CanRunDelete));
    }
}
