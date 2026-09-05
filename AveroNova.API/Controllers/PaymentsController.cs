using System.Security.Claims;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.API.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public sealed class PaymentsController(AppDbContext db) : ControllerBase
{
    private const int CompletedStatus = 1;
    private const int CashMethod = 0;
    private const int OnlineMethod = 5;

    [HttpGet("company/{companyId:guid}")]
    public async Task<IActionResult> GetAll(Guid companyId, CancellationToken ct)
    {
        if (!await CanAccessCompany(companyId, ct)) return Forbid();
        var rows = await db.Payments.AsNoTracking().Where(x => x.CompanyId == companyId && !x.IsDeleted)
            .OrderByDescending(x => x.PaymentDate).ToListAsync(ct);
        return Ok(new { success = true, data = rows });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PaymentSyncRequest r, CancellationToken ct)
    {
        if (!await CanAccessCompany(r.CompanyId, ct)) return Forbid();
        var validation = await ValidateAsync(r, null, ct);
        if (validation is not null) return BadRequest(new { success = false, error = validation });

        var existing = await db.Payments.FirstOrDefaultAsync(x => x.Id == r.Id, ct);
        if (existing is not null) return Ok(new { success = true, data = ToResponse(existing), idempotent = true });
        if (await db.Payments.AnyAsync(x => x.CompanyId == r.CompanyId && x.PaymentNumber == r.PaymentNumber && !x.IsDeleted, ct))
            return Conflict(new { success = false, error = "Payment number already exists." });

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;
        var row = new Payment
        {
            Id = r.Id, CompanyId = r.CompanyId, PaymentNumber = r.PaymentNumber.Trim(), PartyId = r.PartyId,
            PartyName = r.PartyName?.Trim() ?? string.Empty, IsSupplier = r.IsSupplier, InvoiceId = r.InvoiceId,
            InvoiceNumber = r.InvoiceNumber?.Trim() ?? string.Empty, Amount = r.Amount, Method = r.Method,
            PaymentDate = r.PaymentDate, Reference = r.Reference?.Trim() ?? string.Empty, Notes = r.Notes?.Trim() ?? string.Empty,
            Status = r.Status, CreatedAt = now, UpdatedAt = now, SyncVersion = Math.Max(1, r.SyncVersion),
            SyncStatus = RecordSyncStatus.Synced, LastSyncedAt = now
        };
        db.Payments.Add(row);
        await db.SaveChangesAsync(ct);
        await RecalculateInvoiceAsync(row.InvoiceId, row.CompanyId, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { success = true, data = ToResponse(row) });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] PaymentSyncRequest r, CancellationToken ct)
    {
        var row = await db.Payments.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (row is null) return NotFound(new { success = false, error = "Payment not found." });
        if (!await CanAccessCompany(row.CompanyId, ct)) return Forbid();
        if (r.CompanyId != Guid.Empty && r.CompanyId != row.CompanyId)
            return BadRequest(new { success = false, error = "Payment company cannot be changed." });
        if (r.SyncVersion > 0 && r.SyncVersion < row.SyncVersion)
            return Conflict(new { success = false, error = "Payment has newer server changes." });
        var validation = await ValidateAsync(r, id, ct);
        if (validation is not null) return BadRequest(new { success = false, error = validation });

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var oldInvoiceId = row.InvoiceId;
        row.ApplyUpdate(r.PaymentNumber, r.PartyId, r.PartyName ?? string.Empty, r.IsSupplier, r.InvoiceId,
            r.InvoiceNumber ?? string.Empty, r.Amount, r.Method, r.PaymentDate, r.Reference, r.Notes, r.Status);
        row.SyncStatus = RecordSyncStatus.Synced;
        row.LastSyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await RecalculateInvoiceAsync(oldInvoiceId, row.CompanyId, ct);
        if (r.InvoiceId != oldInvoiceId) await RecalculateInvoiceAsync(r.InvoiceId, row.CompanyId, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { success = true, data = ToResponse(row) });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var row = await db.Payments.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (row is null) return Ok(new { success = true });
        if (!await CanAccessCompany(row.CompanyId, ct)) return Forbid();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var invoiceId = row.InvoiceId;
        row.IsDeleted = true;
        row.MarkPendingChange();
        row.SyncStatus = RecordSyncStatus.Synced;
        row.LastSyncedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await RecalculateInvoiceAsync(invoiceId, row.CompanyId, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { success = true });
    }

    private async Task<string?> ValidateAsync(PaymentSyncRequest r, Guid? excludePaymentId, CancellationToken ct)
    {
        if (r.Id == Guid.Empty || r.CompanyId == Guid.Empty || r.Amount <= 0 || string.IsNullOrWhiteSpace(r.PaymentNumber))
            return "Payment id, company, number and positive amount are required.";
        if (r.Method is not CashMethod and not OnlineMethod) return "Payment type must be Cash or Online.";
        if (r.InvoiceId is not Guid invoiceId || invoiceId == Guid.Empty) return "Invoice is required.";
        var invoice = await db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == invoiceId && x.CompanyId == r.CompanyId && !x.IsDeleted, ct);
        if (invoice is null) return "Invoice not found.";
        if (invoice.Status == 5) return "Cancelled invoice cannot receive payments.";
        var paid = await db.Payments.Where(x => x.CompanyId == r.CompanyId && x.InvoiceId == invoiceId && !x.IsDeleted && x.Status == CompletedStatus && (!excludePaymentId.HasValue || x.Id != excludePaymentId.Value))
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        var total = InvoiceMath.GetGrandTotal(invoice.ItemsJson, invoice.DiscountPct, invoice.TaxPct);
        if (paid + r.Amount > total + 0.01m) return "Payment amount cannot exceed invoice due amount.";
        return null;
    }

    private async Task RecalculateInvoiceAsync(Guid? invoiceId, Guid companyId, CancellationToken ct)
    {
        if (invoiceId is not Guid iid || iid == Guid.Empty) return;
        var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.Id == iid && x.CompanyId == companyId && !x.IsDeleted, ct);
        if (invoice is null) return;
        var totalPaid = await db.Payments.Where(x => x.CompanyId == companyId && x.InvoiceId == iid && !x.IsDeleted && x.Status == CompletedStatus)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
        invoice.PaidAmount = totalPaid;
        var grandTotal = InvoiceMath.GetGrandTotal(invoice.ItemsJson, invoice.DiscountPct, invoice.TaxPct);
        if (invoice.Status != 5)
            invoice.Status = totalPaid <= 0 ? invoice.Status : totalPaid >= grandTotal - 0.01m ? 3 : 2;
        invoice.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<bool> CanAccessCompany(Guid companyId, CancellationToken ct)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(raw, out var userId)) return false;
        return await db.UserCompanies.AnyAsync(x => x.UserId == userId && x.CompanyId == companyId && x.IsActive && !x.IsDeleted, ct);
    }

    private static object ToResponse(Payment x) => new { id = x.Id, x.SyncVersion, x.UpdatedAt };

    public sealed class PaymentSyncRequest
    {
        public Guid Id { get; set; } public Guid CompanyId { get; set; } public string PaymentNumber { get; set; } = string.Empty;
        public Guid PartyId { get; set; } public string? PartyName { get; set; } public bool IsSupplier { get; set; }
        public Guid? InvoiceId { get; set; } public string? InvoiceNumber { get; set; } public decimal Amount { get; set; }
        public int Method { get; set; } public DateTime PaymentDate { get; set; } public string? Reference { get; set; }
        public string? Notes { get; set; } public int Status { get; set; } public long SyncVersion { get; set; }
    }

    private static class InvoiceMath
    {
        public static decimal GetGrandTotal(string json, decimal invoiceDiscountPct, decimal invoiceTaxPct)
        {
            try
            {
                var items = System.Text.Json.JsonSerializer.Deserialize<List<Line>>(json) ?? [];
                var subtotal = items.Sum(i => i.UnitPrice * i.Quantity * (1 - i.DiscountPct / 100m));
                var itemTax = items.Sum(i => (i.UnitPrice * i.Quantity * (1 - i.DiscountPct / 100m)) * i.TaxPct / 100m);
                var discount = subtotal * invoiceDiscountPct / 100m;
                var invoiceTax = subtotal * invoiceTaxPct / 100m;
                return subtotal + itemTax + invoiceTax - discount;
            }
            catch { return 0m; }
        }
        private sealed class Line { public decimal UnitPrice { get; set; } public int Quantity { get; set; } public decimal DiscountPct { get; set; } public decimal TaxPct { get; set; } }
    }
}
