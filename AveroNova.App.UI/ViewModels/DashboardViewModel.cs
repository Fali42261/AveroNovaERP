using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace AveroNova.App.UI.ViewModels;

/// <summary>
/// Mock data for the Dashboard. Replace observable properties with
/// real service calls once the back-end services are ready.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    // ── KPI Cards ─────────────────────────────────────────────────────────────

    [ObservableProperty] private string welcomeMessage   = "Welcome to AveroNova";
    [ObservableProperty] private string companyCount     = "1";
    [ObservableProperty] private string userCount        = "4";
    [ObservableProperty] private string salesCount       = "128";
    [ObservableProperty] private string inventoryCount   = "342";

    // ── Financial Summary ─────────────────────────────────────────────────────

    [ObservableProperty] private string totalRevenue     = "₹ 8,42,500";
    [ObservableProperty] private string totalPurchase    = "₹ 3,18,200";
    [ObservableProperty] private string totalOutstanding = "₹ 1,05,000";
    [ObservableProperty] private string totalCustomers   = "56";
    [ObservableProperty] private string totalProducts    = "342";
    [ObservableProperty] private string totalSuppliers   = "18";

    // ── Trend badges ─────────────────────────────────────────────────────────

    [ObservableProperty] private string revenueTrend   = "+12% this month";
    [ObservableProperty] private string purchaseTrend  = "+5% this month";
    [ObservableProperty] private string customersTrend = "+3 new this week";

    // ── Recent Transactions (mock) ────────────────────────────────────────────

    [ObservableProperty]
    private ObservableCollection<RecentTransaction> recentTransactions = new()
    {
        new("INV-2026-001", "Acme Corp",        "₹ 42,000",  "Paid",     "#10B981", "#ECFDF5", "#A7F3D0"),
        new("INV-2026-002", "Global Traders",   "₹ 18,500",  "Pending",  "#D97706", "#FFFBEB", "#FDE68A"),
        new("PO-2026-015",  "Nova Supplies",    "₹ 95,000",  "Received", "#2563EB", "#EFF6FF", "#BFDBFE"),
        new("INV-2026-003", "Sunrise Retail",   "₹ 7,200",   "Overdue",  "#EF4444", "#FEF2F2", "#FECACA"),
        new("INV-2026-004", "TechMart India",   "₹ 23,800",  "Paid",     "#10B981", "#ECFDF5", "#A7F3D0"),
    };

    public DashboardViewModel()
    {
        // Greeting adapts to time of day
        var hour = DateTime.Now.Hour;
        var greeting = hour < 12 ? "Good Morning" : hour < 17 ? "Good Afternoon" : "Good Evening";
        WelcomeMessage = $"{greeting}, Admin";
    }
}

/// <summary>
/// Represents one row in the Recent Transactions table.
/// </summary>
public class RecentTransaction
{
    public string Reference    { get; }
    public string Party        { get; }
    public string Amount       { get; }
    public string Status       { get; }
    public string StatusColor  { get; }
    public string BadgeBg      { get; }
    public string BadgeBorder  { get; }

    public RecentTransaction(
        string reference, string party, string amount,
        string status, string statusColor, string badgeBg, string badgeBorder)
    {
        Reference   = reference;
        Party       = party;
        Amount      = amount;
        Status      = status;
        StatusColor = statusColor;
        BadgeBg     = badgeBg;
        BadgeBorder = badgeBorder;
    }
}
