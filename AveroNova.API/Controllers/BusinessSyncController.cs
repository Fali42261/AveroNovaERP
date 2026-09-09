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

namespace AveroNova.API.Controllers;

[ApiController]
[Authorize]
[Route("api/sync/business")]
public sealed class BusinessSyncController : ControllerBase
{
    private static readonly HashSet<string> SupportedTypes =
        new(["Invoice", "Purchase", "Payment", "Supplier", "Product", "StockMovement"], StringComparer.OrdinalIgnoreCase);

    private readonly AppDbContext _db;

    public BusinessSyncController(AppDbContext db) => _db = db;

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

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
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
                await ApplyAsync(item, now, cancellationToken);
                results.Add(new BusinessSyncItemResult
                {
                    QueueId = item.QueueId,
                    EntityId = item.EntityId,
                    EntityType = item.EntityType,
                    Success = true,
                    ServerUpdatedAtUtc = now
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(new
            {
                success = true,
                data = new BusinessSyncBatchResponse { ServerTimeUtc = now, Items = results }
            });
        }
        catch (BusinessSyncValidationException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    private async Task ApplyAsync(BusinessSyncItemRequest item, DateTime now, CancellationToken cancellationToken)
    {
        var record = await _db.SyncQueueItems
            .Where(x => x.CompanyId == item.CompanyId
                        && x.EntityType == item.EntityType
                        && x.EntityId == item.EntityId)
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (item.Operation == SyncOperation.Delete)
        {
            if ((item.EntityType.Equals("Invoice", StringComparison.OrdinalIgnoreCase)
                 || item.EntityType.Equals("Purchase", StringComparison.OrdinalIgnoreCase))
                && await HasLinkedPaymentsAsync(item.CompanyId, item.EntityId,
                    item.EntityType.Equals("Purchase", StringComparison.OrdinalIgnoreCase), cancellationToken))
                throw new BusinessSyncValidationException("Delete linked payments before deleting this invoice.");

            Guid? affectedInvoice = null;
            var affectedType = "Invoice";
            if (record is not null && item.EntityType.Equals("Payment", StringComparison.OrdinalIgnoreCase))
            {
                affectedInvoice = ReadGuid(record.PayloadJson, "InvoiceId");
                affectedType = ReadBool(record.PayloadJson, "IsSupplier") ? "Purchase" : "Invoice";
            }

            if (record is not null)
            {
                record.IsDeleted = true;
                record.PayloadJson = null;
                MarkSynced(record, item.Operation, now);
                await _db.SaveChangesAsync(cancellationToken);
            }
            if (affectedInvoice is Guid invoiceId)
                await ReconcileDocumentAsync(item.CompanyId, affectedType, invoiceId, now, cancellationToken);
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
        if (ReadBool(payload, "IsSupplier"))
            throw new BusinessSyncValidationException("Supplier payments cannot be linked to a sales invoice.");

        var isSupplier = ReadBool(payload, "IsSupplier");
        var documentType = isSupplier ? "Purchase" : "Invoice";
        var invoice = await FindRecordAsync(item.CompanyId, documentType, linkedInvoiceId, cancellationToken);
        if (invoice is null || invoice.IsDeleted || string.IsNullOrWhiteSpace(invoice.PayloadJson))
            throw new BusinessSyncValidationException($"Linked {documentType.ToLowerInvariant()} does not exist on the server.");

        var total = ReadDecimal(invoice.PayloadJson, "GrandTotal");
        var invoiceStatus = ReadInt(invoice.PayloadJson, "Status");
        if (invoiceStatus == 0 || (!isSupplier && invoiceStatus == 5) || (isSupplier && invoiceStatus == 4))
            throw new BusinessSyncValidationException("Payments can only be applied to an active posted document.");
        var otherPaid = await CompletedPaymentTotalAsync(
            item.CompanyId, linkedInvoiceId, item.EntityId, isSupplier, cancellationToken);
        if (status == 1 && otherPaid + amount > total)
            throw new BusinessSyncValidationException("Payment exceeds the invoice outstanding balance.");
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
        Set(payload, "PaidAmount", Math.Min(total, paid));

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
}
