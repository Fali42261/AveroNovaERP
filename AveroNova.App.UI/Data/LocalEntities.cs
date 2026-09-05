namespace AveroNova.App.UI.Data;

public enum LocalInstallationStatus
{
    NotRegistered = 0,
    Registered = 1
}

/// <summary>
/// Single-row local installation identity. Distinct from User, Company, and DeviceId.
/// </summary>
public class LocalInstallationEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstallationId { get; set; }
    public LocalInstallationStatus Status { get; set; } = LocalInstallationStatus.NotRegistered;
    public DateTime? RegisteredAtUtc { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public Guid? CompanyId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class LocalSessionEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid InstallationId { get; set; }
    public Guid ServerSessionId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime EstablishedAtUtc { get; set; }
    public DateTime LastAuthenticatedAtUtc { get; set; }
    public DateTime? LastValidatedAtUtc { get; set; }
    public DateTime OfflineExpiresAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}

public class LocalUserEntity
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public long SyncVersion { get; set; } = 1;
}

public class LocalCompanyEntity
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public long SyncVersion { get; set; } = 1;
}

public class LocalUserCompanyEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsDefault { get; set; }
    public bool IsOwner { get; set; }
    public bool IsActive { get; set; } = true;
}

public class LocalRoleEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public string RoleName { get; set; } = string.Empty;
}

public class LocalPermissionEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public string PermissionName { get; set; } = string.Empty;
}

public class LocalSubscriptionEntity
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string PlanName { get; set; } = "Starter";
    public bool IsTrial { get; set; } = true;
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public bool IsActive { get; set; } = true;
}

public class LocalLicenseEntity
{
    public Guid Id { get; set; }
    public Guid LicenseId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? CompanyId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Plan { get; set; } = "Starter";
    public int Status { get; set; }
    public bool IsTrial { get; set; } = true;
    public DateTime TrialStartDateUtc { get; set; }
    public DateTime TrialEndDateUtc { get; set; }
    public DateTime? ExpiryDateUtc { get; set; }
    public DateTime? LastValidatedAtUtc { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
    public DateTime? LastKnownServerTimeUtc { get; set; }
    public DateTime LastKnownTrustedTimeUtc { get; set; } = DateTime.UtcNow;
    public bool IsServerAuthoritative { get; set; }
    public bool ClockRollbackDetected { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class LocalCustomerEntity
{
    public Guid Id { get; set; }
    public Guid? ServerId { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int Status { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal TotalPurchases { get; set; }
    public int SyncStatus { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncedAtUtc { get; set; }
    public string? SyncError { get; set; }
}

public class LocalProductEntity
{
    public Guid Id { get; set; }
    public Guid? ServerId { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Unit { get; set; } = "pcs";
    public decimal PurchasePrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal TaxPercent { get; set; }
    public int Stock { get; set; }
    public int MinimumStock { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Status { get; set; }
    public int SyncStatus { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncedAtUtc { get; set; }
    public string? SyncError { get; set; }
}

public class LocalStockMovementEntity
{
    public Guid Id { get; set; }
    public Guid? ServerId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public int Type { get; set; }
    public int Quantity { get; set; }
    public int StockBefore { get; set; }
    public int StockAfter { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public int SyncStatus { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncedAtUtc { get; set; }
    public string? SyncError { get; set; }
}

public class LocalInvoiceEntity
{
    public Guid Id { get; set; }
    public Guid? ServerId { get; set; }
    public Guid CompanyId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public string ItemsJson { get; set; } = "[]";
    public decimal DiscountPct { get; set; }
    public decimal TaxPct { get; set; }
    public int PaymentMethod { get; set; }
    public string Notes { get; set; } = string.Empty;
    public int Status { get; set; }
    public decimal PaidAmount { get; set; }
    public int SyncStatus { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncedAtUtc { get; set; }
    public string? SyncError { get; set; }
}

public class LocalPaymentEntity
{
    public Guid Id { get; set; }
    public Guid? ServerId { get; set; }
    public Guid CompanyId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public Guid PartyId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public bool IsSupplier { get; set; }
    public Guid? InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Method { get; set; }
    public DateTime PaymentDate { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int Status { get; set; }
    public int SyncStatus { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncedAtUtc { get; set; }
    public string? SyncError { get; set; }
}

public class LocalSyncQueueEntity
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public int Operation { get; set; }
    public int Status { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? SyncedAt { get; set; }
    public string? Error { get; set; }
    public Guid? CompanyId { get; set; }
    /// <summary>Non-secret registration metadata JSON (never password).</summary>
    public string? PayloadJson { get; set; }
}

public class LocalSchemaInfoEntity
{
    public int Id { get; set; } = 1;
    public int SchemaVersion { get; set; }
    public DateTime AppliedAtUtc { get; set; }
}
