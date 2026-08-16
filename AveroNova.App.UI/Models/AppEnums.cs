namespace AveroNova.App.UI.Models;

// ═══════════════════════════════════════════════════════════════
//  AVERONOVA ERP — SHARED ENUMERATIONS
//  Used across Models, ViewModels, and Services.
// ═══════════════════════════════════════════════════════════════

public enum ConnectivityStatus
{
    Online,
    Offline,
    Syncing,
    Synced,
    SyncFailed,
    PendingSync
}

public enum SyncStatus
{
    Synced,
    PendingSync,
    SyncFailed,
    Local         // created offline, never synced
}

public enum UserStatus  { Active, Inactive, Suspended }
public enum CompanyStatus { Active, Inactive, Suspended }
public enum CustomerStatus { Active, Inactive, Blocked }
public enum ProductStatus { Active, Inactive, Discontinued }
public enum InvoiceStatus { Draft, Sent, PartialPaid, Paid, Overdue, Cancelled }
public enum PurchaseStatus { Draft, Ordered, PartialReceived, Received, Cancelled }
public enum PaymentStatus { Pending, Completed, Failed, Refunded, Cancelled }
public enum ReturnStatus { Pending, Approved, Rejected, Completed }
public enum ExpenseStatus { Pending, Approved, Rejected, Paid }
public enum SubscriptionStatus { Active, Expired, Trial, Cancelled, PendingRenewal }
public enum BillingCycle { Monthly, Yearly }
public enum StockMovementType { In, Out, Adjustment, Transfer, Return }

public enum PaymentMethod
{
    Cash,
    BankTransfer,
    CreditCard,
    DebitCard,
    Cheque,
    Online,
    Other
}

public enum ThemeMode { Light, Dark, System }

public enum NotificationCategory
{
    Invoice,
    Payment,
    Inventory,
    System,
    Sync,
    Subscription
}
