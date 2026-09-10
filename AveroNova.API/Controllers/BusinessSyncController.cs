using System.Security.Claims;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AveroNova.Application.DTOs.Sync;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Infrastructure.Auth;
using AveroNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AveroNova.API.Controllers;

[ApiController]
[Authorize]
[Route("api/sync/business")]
public sealed class BusinessSyncController : ControllerBase
{
    private static readonly HashSet<string> SupportedTypes =
        new([
            "AppSettings", "Company", "CompanyUser", "Customer", "Expense", "Invoice",
            "Notification", "Payment", "Product", "Purchase", "PurchaseReturn", "Role",
            "SalesReturn", "StockMovement", "Subscription", "SubscriptionPayment", "Supplier"
        ], StringComparer.OrdinalIgnoreCase);

    private readonly AppDbContext _db;
    private readonly ILogger<BusinessSyncController> _logger;

    public BusinessSyncController(AppDbContext db, ILogger<BusinessSyncController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost("push")]
    public async Task<IActionResult> Push(
        [FromBody] BusinessSyncBatchRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCompanyId(User, out var companyId))
            return Forbid();
        if (request?.Items is null || request.Items.Count is 0 or > 200)
            return BadRequest(new { success = false, error = "Sync batch must contain 1 to 200 items." });
        if (request.Items.Any(i => string.IsNullOrWhiteSpace(i.EntityType)))
            return BadRequest(new { success = false, error = "Every sync item must specify an entity type." });
        foreach (var item in request.Items)
            item.EntityType = SupportedTypes.FirstOrDefault(x => x.Equals(item.EntityType, StringComparison.OrdinalIgnoreCase)) ?? item.EntityType;
        if (request.Items.Any(i => i.CompanyId != companyId))
            return Forbid();
        if (request.Items.Any(i => !SupportedTypes.Contains(i.EntityType)))
            return BadRequest(new { success = false, error = "Sync batch contains an unsupported entity type." });
        if (request.Items.Any(i => i.QueueId == Guid.Empty || i.EntityId == Guid.Empty))
            return BadRequest(new { success = false, error = "Sync queue and entity IDs are required." });
        if (request.Items.Any(i => !Enum.IsDefined(i.Operation)))
            return BadRequest(new { success = false, error = "Sync batch contains an invalid operation." });

        var now = DateTime.UtcNow;
        var results = new List<BusinessSyncItemResult>(request.Items.Count);
        var ordered = request.Items
                .OrderBy(i => i.EntityType.Equals("Invoice", StringComparison.OrdinalIgnoreCase)
                              || i.EntityType.Equals("Purchase", StringComparison.OrdinalIgnoreCase) ? 0
                    : i.EntityType.Equals("Payment", StringComparison.OrdinalIgnoreCase) ? 2 : 1)
                .ThenBy(i => i.ClientUpdatedAtUtc)
                .ToList();

        foreach (var item in ordered)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await ApplyAsync(item, now, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);
                var record = await FindRecordAsync(item.CompanyId, item.EntityType, item.EntityId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                results.Add(new BusinessSyncItemResult
                {
                    QueueId = item.QueueId,
                    EntityId = item.EntityId,
                    EntityType = item.EntityType,
                    Success = true,
                    ServerUpdatedAtUtc = now,
                    ServerVersion = record?.SyncVersion ?? 0
                });
            }
            catch (BusinessSyncConflictException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                results.Add(new BusinessSyncItemResult
                {
                    QueueId = item.QueueId,
                    EntityId = item.EntityId,
                    EntityType = item.EntityType,
                    Success = false,
                    Conflict = true,
                    Error = "The server record changed after this device last synchronized.",
                    ServerUpdatedAtUtc = ex.ServerUpdatedAtUtc,
                    ServerVersion = ex.ServerVersion,
                    ServerPayloadJson = ex.ServerPayloadJson
                });
            }
            catch (BusinessSyncValidationException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                results.Add(new BusinessSyncItemResult
                {
                    QueueId = item.QueueId,
                    EntityId = item.EntityId,
                    EntityType = item.EntityType,
                    Success = false,
                    Error = ex.Message,
                    ServerUpdatedAtUtc = now
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                _logger.LogError(ex, "Business sync failed for {EntityType}/{EntityId}.", item.EntityType, item.EntityId);
                results.Add(new BusinessSyncItemResult
                {
                    QueueId = item.QueueId,
                    EntityId = item.EntityId,
                    EntityType = item.EntityType,
                    Success = false,
                    Error = "The record could not be synchronized.",
                    ServerUpdatedAtUtc = now
                });
            }
        }

        return Ok(new
        {
            success = true,
            data = new BusinessSyncBatchResponse { ServerTimeUtc = now, Items = results }
        });
    }

    private async Task ApplyAsync(BusinessSyncItemRequest item, DateTime now, CancellationToken cancellationToken)
    {
        var record = await _db.SyncQueueItems
            .Where(x => x.CompanyId == item.CompanyId
                        && x.EntityType == item.EntityType
                        && x.EntityId == item.EntityId)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (record is not null
            && item.ExpectedServerVersion > 0
            && item.ExpectedServerVersion != record.SyncVersion)
            throw new BusinessSyncConflictException(record);

        if (item.Operation == SyncOperation.Delete)
        {
            if ((item.EntityType.Equals("Invoice", StringComparison.OrdinalIgnoreCase)
                 || item.EntityType.Equals("Purchase", StringComparison.OrdinalIgnoreCase))
                && await HasLinkedPaymentsAsync(item.CompanyId, item.EntityId,
                    item.EntityType.Equals("Purchase", StringComparison.OrdinalIgnoreCase), cancellationToken))
                throw new BusinessSyncValidationException("Delete linked payments before deleting this invoice.");
            if (item.EntityType.Equals("Purchase", StringComparison.OrdinalIgnoreCase)
                && await HasPurchaseReturnsAsync(item.CompanyId, item.EntityId, cancellationToken))
                throw new BusinessSyncValidationException("Delete purchase returns before deleting this purchase.");

            Guid? affectedInvoice = null;
            var affectedType = "Invoice";
            if (record is not null && item.EntityType.Equals("Payment", StringComparison.OrdinalIgnoreCase))
            {
                affectedInvoice = ReadGuid(record.PayloadJson, "InvoiceId");
                affectedType = ReadBool(record.PayloadJson, "IsSupplier") ? "Purchase" : "Invoice";
            }
            Guid? affectedPurchase = record is not null && item.EntityType.Equals("PurchaseReturn", StringComparison.OrdinalIgnoreCase)
                ? ReadGuid(record.PayloadJson, "PurchaseId") : null;

            record ??= new SyncQueueItem
            {
                Id = Guid.NewGuid(),
                EntityType = item.EntityType,
                EntityId = item.EntityId,
                CompanyId = item.CompanyId,
                CreatedAt = now
            };
            if (_db.Entry(record).State == EntityState.Detached)
                _db.SyncQueueItems.Add(record);
            record.IsDeleted = true;
            record.PayloadJson = null;
            MarkSynced(record, item.Operation, now);
            await _db.SaveChangesAsync(cancellationToken);
            if (affectedInvoice is Guid invoiceId)
                await ReconcileDocumentAsync(item.CompanyId, affectedType, invoiceId, now, cancellationToken);
            if (affectedPurchase is Guid purchaseId)
                await ReconcileDocumentAsync(item.CompanyId, "Purchase", purchaseId, now, cancellationToken);
            return;
        }

        var payload = ParsePayload(item);
        RequireIdentity(payload, item);

        if (item.EntityType.Equals("Invoice", StringComparison.OrdinalIgnoreCase))
            ValidateInvoice(payload);
        else if (item.EntityType.Equals("Purchase", StringComparison.OrdinalIgnoreCase))
            ValidatePurchase(payload);
        else if (item.EntityType.Equals("Payment", StringComparison.OrdinalIgnoreCase))
            await ValidatePaymentAsync(item, payload, cancellationToken);
        else if (item.EntityType.Equals("PurchaseReturn", StringComparison.OrdinalIgnoreCase))
            await ValidatePurchaseReturnAsync(item, payload, cancellationToken);

        record ??= new SyncQueueItem
        {
            Id = Guid.NewGuid(),
            EntityType = item.EntityType,
            EntityId = item.EntityId,
            CompanyId = item.CompanyId,
            CreatedAt = now
        };
        if (_db.Entry(record).State == EntityState.Detached)
            _db.SyncQueueItems.Add(record);

        Guid? oldInvoiceId = item.EntityType.Equals("Payment", StringComparison.OrdinalIgnoreCase)
            ? ReadGuid(record.PayloadJson, "InvoiceId")
            : null;
        var oldDocumentType = item.EntityType.Equals("Payment", StringComparison.OrdinalIgnoreCase)
                              && ReadBool(record.PayloadJson, "IsSupplier") ? "Purchase" : "Invoice";
        var oldPurchaseId = item.EntityType.Equals("PurchaseReturn", StringComparison.OrdinalIgnoreCase)
            ? ReadGuid(record.PayloadJson, "PurchaseId") : null;
        record.PayloadJson = payload.ToJsonString();
        record.IsDeleted = false;
        MarkSynced(record, item.Operation, now);
        await _db.SaveChangesAsync(cancellationToken);

        if (item.EntityType.Equals("Invoice", StringComparison.OrdinalIgnoreCase))
        {
            await ReconcileDocumentAsync(item.CompanyId, "Invoice", item.EntityId, now, cancellationToken);
        }
        else if (item.EntityType.Equals("Purchase", StringComparison.OrdinalIgnoreCase))
        {
            await ReconcileDocumentAsync(item.CompanyId, "Purchase", item.EntityId, now, cancellationToken);
        }
        else if (item.EntityType.Equals("Payment", StringComparison.OrdinalIgnoreCase))
        {
            var newInvoiceId = ReadGuid(record.PayloadJson, "InvoiceId");
            var newType = ReadBool(record.PayloadJson, "IsSupplier") ? "Purchase" : "Invoice";
            if (oldInvoiceId is Guid previous && (previous != newInvoiceId || oldDocumentType != newType))
                await ReconcileDocumentAsync(item.CompanyId, oldDocumentType, previous, now, cancellationToken);
            if (newInvoiceId is Guid current)
                await ReconcileDocumentAsync(item.CompanyId, newType, current, now, cancellationToken);
        }
        else if (item.EntityType.Equals("PurchaseReturn", StringComparison.OrdinalIgnoreCase))
        {
            var newPurchaseId = ReadGuid(record.PayloadJson, "PurchaseId");
            if (oldPurchaseId is Guid previous && previous != newPurchaseId)
                await ReconcileDocumentAsync(item.CompanyId, "Purchase", previous, now, cancellationToken);
            if (newPurchaseId is Guid current)
                await ReconcileDocumentAsync(item.CompanyId, "Purchase", current, now, cancellationToken);
        }
    }

    private static void ValidateInvoice(JsonObject payload)
    {
        if (ReadDecimal(payload, "GrandTotal") < 0)
            throw new BusinessSyncValidationException("Invoice total cannot be negative.");
    }

    private static void ValidatePurchase(JsonObject payload)
    {
        if (ReadDecimal(payload, "GrandTotal") < 0)
            throw new BusinessSyncValidationException("Purchase total cannot be negative.");
        var status = ReadInt(payload, "Status");
        if (status is < 0 or > 4)
            throw new BusinessSyncValidationException("Purchase status is invalid.");
    }

    private async Task ValidatePaymentAsync(
        BusinessSyncItemRequest item,
        JsonObject payload,
        CancellationToken cancellationToken)
    {
        var amount = ReadDecimal(payload, "Amount");
        if (amount <= 0)
            throw new BusinessSyncValidationException("Payment amount must be greater than zero.");
        var status = ReadInt(payload, "Status");
        if (status is < 0 or > 4)
            throw new BusinessSyncValidationException("Payment status is invalid.");

        var invoiceId = ReadGuid(payload, "InvoiceId");
        if (invoiceId is not Guid linkedInvoiceId)
            return;
        var isSupplier = ReadBool(payload, "IsSupplier");
        var documentType = isSupplier ? "Purchase" : "Invoice";
        var invoice = await FindRecordAsync(item.CompanyId, documentType, linkedInvoiceId, cancellationToken);
        if (invoice is null || invoice.IsDeleted || string.IsNullOrWhiteSpace(invoice.PayloadJson))
            throw new BusinessSyncValidationException($"Linked {documentType.ToLowerInvariant()} does not exist on the server.");

        var total = ReadDecimal(invoice.PayloadJson, "GrandTotal");
        if (isSupplier) total -= await CompletedReturnCreditTotalAsync(item.CompanyId, linkedInvoiceId, null, cancellationToken);
        var invoiceStatus = ReadInt(invoice.PayloadJson, "Status");
        if (invoiceStatus == 0 || (!isSupplier && invoiceStatus == 5) || (isSupplier && invoiceStatus == 4))
            throw new BusinessSyncValidationException("Payments can only be applied to an active posted document.");
        var otherPaid = await CompletedPaymentTotalAsync(
            item.CompanyId, linkedInvoiceId, item.EntityId, isSupplier, cancellationToken);
        if (status == 1 && otherPaid + amount > total)
            throw new BusinessSyncValidationException("Payment exceeds the invoice outstanding balance.");
    }

    private async Task ValidatePurchaseReturnAsync(BusinessSyncItemRequest item, JsonObject payload, CancellationToken cancellationToken)
    {
        var status = ReadInt(payload, "Status");
        if (status is < 0 or > 3) throw new BusinessSyncValidationException("Purchase return status is invalid.");
        var refund = ReadDecimal(payload, "RefundAmount");
        if (refund <= 0) throw new BusinessSyncValidationException("Purchase return refund must be greater than zero.");
        var purchaseId = ReadGuid(payload, "PurchaseId");
        if (purchaseId is not Guid linkedPurchaseId) throw new BusinessSyncValidationException("Purchase return must reference a purchase.");
        var purchase = await FindRecordAsync(item.CompanyId, "Purchase", linkedPurchaseId, cancellationToken);
        if (purchase is null || purchase.IsDeleted || string.IsNullOrWhiteSpace(purchase.PayloadJson))
            throw new BusinessSyncValidationException("Linked purchase does not exist on the server.");
        if (ReadInt(purchase.PayloadJson, "Status") != 3)
            throw new BusinessSyncValidationException("Only a received purchase can be returned.");

        var requested = ReadLineQuantities(payload);
        if (requested.Count == 0 || requested.Values.Any(x => x <= 0))
            throw new BusinessSyncValidationException("Purchase return items are invalid.");
        var purchased = ReadLineQuantities(ParseObject(purchase.PayloadJson));
        if (requested.Keys.Any(id => !purchased.ContainsKey(id)))
            throw new BusinessSyncValidationException("A return item does not belong to the purchase.");
        var otherRows = await _db.SyncQueueItems.Where(x => x.CompanyId == item.CompanyId && x.EntityType == "PurchaseReturn"
            && x.EntityId != item.EntityId && !x.IsDeleted).Select(x => x.PayloadJson).ToListAsync(cancellationToken);
        var reserved = otherRows.Where(x => ReadGuid(x, "PurchaseId") == linkedPurchaseId && ReadInt(x, "Status") != 2)
            .SelectMany(x => ReadLineQuantities(ParseObject(x!))).GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.Sum(v => v.Value));
        foreach (var (productId, quantity) in requested)
            if (quantity + reserved.GetValueOrDefault(productId) > purchased[productId])
                throw new BusinessSyncValidationException("Return quantity exceeds the purchased quantity.");
        var otherRefunds = otherRows.Where(x => ReadGuid(x, "PurchaseId") == linkedPurchaseId && ReadInt(x, "Status") != 2)
            .Sum(x => ReadDecimal(x, "RefundAmount"));
        if (otherRefunds + refund > ReadDecimal(purchase.PayloadJson, "GrandTotal"))
            throw new BusinessSyncValidationException("Purchase return credits exceed the purchase total.");
    }

    private async Task ReconcileDocumentAsync(
        Guid companyId,
        string documentType,
        Guid invoiceId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var invoice = await FindRecordAsync(companyId, documentType, invoiceId, cancellationToken);
        if (invoice is null || invoice.IsDeleted || string.IsNullOrWhiteSpace(invoice.PayloadJson))
            return;

        var payload = ParseObject(invoice.PayloadJson);
        var total = ReadDecimal(payload, "GrandTotal");
        var paid = await CompletedPaymentTotalAsync(companyId, invoiceId, null, documentType == "Purchase", cancellationToken);
        if (documentType == "Purchase")
        {
            Set(payload, "PaidAmount", paid);
            Set(payload, "ReturnCreditAmount", await CompletedReturnCreditTotalAsync(companyId, invoiceId, null, cancellationToken));
        }
        else
        {
            Set(payload, "PaidAmount", Math.Min(total, paid));
        }

        var status = ReadInt(payload, "Status");
        if (documentType == "Invoice" && status is not 0 and not 5)
        {
            var dueDate = ReadDateTime(payload, "DueDate");
            Set(payload, "Status", paid >= total && total > 0
                ? 3
                : paid > 0
                    ? 2
                    : dueDate is DateTime due && due.Date < DateTime.UtcNow.Date ? 4 : 1);
        }

        invoice.PayloadJson = payload.ToJsonString();
        MarkSynced(invoice, SyncOperation.Update, now);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<decimal> CompletedPaymentTotalAsync(
        Guid companyId,
        Guid invoiceId,
        Guid? excludingPaymentId,
        bool isSupplier,
        CancellationToken cancellationToken)
    {
        var query = _db.SyncQueueItems.Where(x =>
            x.CompanyId == companyId && x.EntityType == "Payment" && !x.IsDeleted);
        if (excludingPaymentId is Guid excluded)
            query = query.Where(x => x.EntityId != excluded);
        var records = await query.ToListAsync(cancellationToken);
        return records
            .Where(x => ReadGuid(x.PayloadJson, "InvoiceId") == invoiceId
                        && ReadBool(x.PayloadJson, "IsSupplier") == isSupplier
                        && ReadInt(x.PayloadJson, "Status") == 1)
            .Sum(x => ReadDecimal(x.PayloadJson, "Amount"));
    }

    private async Task<bool> HasLinkedPaymentsAsync(Guid companyId, Guid invoiceId, bool isSupplier, CancellationToken cancellationToken)
        => (await _db.SyncQueueItems
                .Where(x => x.CompanyId == companyId && x.EntityType == "Payment" && !x.IsDeleted)
                .Select(x => x.PayloadJson)
                .ToListAsync(cancellationToken))
            .Any(payload => ReadGuid(payload, "InvoiceId") == invoiceId && ReadBool(payload, "IsSupplier") == isSupplier);

    private async Task<decimal> CompletedReturnCreditTotalAsync(Guid companyId, Guid purchaseId, Guid? excludingReturnId, CancellationToken cancellationToken)
    {
        var query = _db.SyncQueueItems.Where(x => x.CompanyId == companyId && x.EntityType == "PurchaseReturn" && !x.IsDeleted);
        if (excludingReturnId is Guid excluded) query = query.Where(x => x.EntityId != excluded);
        var rows = await query.Select(x => x.PayloadJson).ToListAsync(cancellationToken);
        return rows.Where(x => ReadGuid(x, "PurchaseId") == purchaseId && ReadInt(x, "Status") == 3)
            .Sum(x => ReadDecimal(x, "RefundAmount"));
    }

    private async Task<bool> HasPurchaseReturnsAsync(Guid companyId, Guid purchaseId, CancellationToken cancellationToken)
        => (await _db.SyncQueueItems.Where(x => x.CompanyId == companyId && x.EntityType == "PurchaseReturn" && !x.IsDeleted)
                .Select(x => x.PayloadJson).ToListAsync(cancellationToken))
            .Any(x => ReadGuid(x, "PurchaseId") == purchaseId);

    private Task<SyncQueueItem?> FindRecordAsync(
        Guid companyId,
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken)
        => _db.SyncQueueItems.FirstOrDefaultAsync(x =>
            x.CompanyId == companyId && x.EntityType == entityType && x.EntityId == entityId,
            cancellationToken);

    private static void MarkSynced(SyncQueueItem record, SyncOperation operation, DateTime now)
    {
        record.Operation = operation;
        record.QueueStatus = RecordSyncStatus.Synced;
        record.SyncStatus = RecordSyncStatus.Synced;
        record.Error = null;
        record.LastAttemptAt = now;
        record.LastSyncedAt = now;
        record.UpdatedAt = now;
        record.SyncVersion++;
    }

    private static JsonObject ParsePayload(BusinessSyncItemRequest item)
    {
        if (string.IsNullOrWhiteSpace(item.PayloadJson))
            throw new BusinessSyncValidationException($"{item.EntityType} payload is missing.");
        try { return ParseObject(item.PayloadJson); }
        catch (JsonException) { throw new BusinessSyncValidationException($"{item.EntityType} payload is invalid."); }
    }

    private static JsonObject ParseObject(string json)
        => JsonNode.Parse(json) as JsonObject
           ?? throw new JsonException("Payload must be a JSON object.");

    private static void RequireIdentity(JsonObject payload, BusinessSyncItemRequest item)
    {
        if (ReadGuid(payload, "Id") != item.EntityId || ReadGuid(payload, "CompanyId") != item.CompanyId)
            throw new BusinessSyncValidationException("Sync payload identity does not match its envelope.");
    }

    private static JsonNode? Find(JsonObject value, string name)
        => value.FirstOrDefault(p => p.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
    private static Guid? ReadGuid(JsonObject value, string name)
        => Guid.TryParse(Find(value, name)?.ToString(), out var id) ? id : null;
    private static Guid? ReadGuid(string? json, string name)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return ReadGuid(ParseObject(json), name); } catch { return null; }
    }
    private static decimal ReadDecimal(JsonObject value, string name)
        => decimal.TryParse(Find(value, name)?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    private static decimal ReadDecimal(string? json, string name)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        try { return ReadDecimal(ParseObject(json), name); } catch { return 0; }
    }
    private static int ReadInt(JsonObject value, string name)
        => int.TryParse(Find(value, name)?.ToString(), out var result) ? result : 0;
    private static int ReadInt(string? json, string name)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;
        try { return ReadInt(ParseObject(json), name); } catch { return 0; }
    }
    private static DateTime? ReadDateTime(JsonObject value, string name)
        => DateTime.TryParse(
            Find(value, name)?.ToString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var result)
            ? result
            : null;
    private static bool ReadBool(JsonObject value, string name)
        => bool.TryParse(Find(value, name)?.ToString(), out var result) && result;
    private static bool ReadBool(string? json, string name)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try { return ReadBool(ParseObject(json), name); } catch { return false; }
    }
    private static Dictionary<Guid, int> ReadLineQuantities(JsonObject payload)
    {
        var raw = Find(payload, "ItemsJson")?.ToString();
        if (string.IsNullOrWhiteSpace(raw)) return [];
        try
        {
            var items = JsonNode.Parse(raw) as JsonArray;
            if (items is null) return [];
            return items.OfType<JsonObject>()
                .Select(x => (ProductId: ReadGuid(x, "ProductId"), Quantity: ReadInt(x, "Quantity")))
                .Where(x => x.ProductId.HasValue)
                .GroupBy(x => x.ProductId!.Value)
                .ToDictionary(x => x.Key, x => x.Sum(v => v.Quantity));
        }
        catch (JsonException) { return []; }
    }
    private static void Set(JsonObject value, string name, decimal result) => SetNode(value, name, JsonValue.Create(result));
    private static void Set(JsonObject value, string name, int result) => SetNode(value, name, JsonValue.Create(result));
    private static void SetNode(JsonObject value, string name, JsonNode? result)
    {
        var key = value.FirstOrDefault(p => p.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Key ?? name;
        value[key] = result;
    }

    private static bool TryGetCompanyId(ClaimsPrincipal user, out Guid companyId)
        => Guid.TryParse(user.FindFirstValue(JwtTokenService.CompanyIdClaim), out companyId);

    private sealed class BusinessSyncValidationException(string message) : Exception(message);

    private sealed class BusinessSyncConflictException : Exception
    {
        public BusinessSyncConflictException(SyncQueueItem record)
        {
            ServerVersion = record.SyncVersion;
            ServerUpdatedAtUtc = record.UpdatedAt ?? record.CreatedAt;
            ServerPayloadJson = record.PayloadJson;
        }

        public long ServerVersion { get; }
        public DateTime ServerUpdatedAtUtc { get; }
        public string? ServerPayloadJson { get; }
    }
}
