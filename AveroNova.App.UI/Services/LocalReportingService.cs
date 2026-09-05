using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services;

public sealed class LocalReportingService : IReportingService
{
    private readonly IDbContextFactory<LocalAppDbContext> _factory;
    private readonly IAppSessionContext _session;

    public LocalReportingService(IDbContextFactory<LocalAppDbContext> factory, IAppSessionContext session)
    {
        _factory = factory;
        _session = session;
    }

    public async Task<(FinancialReportSummary? Summary, string? Error)> GetSummaryAsync(
        Guid companyId,
        ReportPeriod period,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty || _session.CurrentCompanyId != companyId)
            return (null, "You do not have access to this company.");
        if (period.From.Date > period.To.Date)
            return (null, "From date cannot be after To date.");

        var from = period.From.Date;
        var through = period.To.Date.AddDays(1);
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        var invoices = await db.Invoices.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.InvoiceDate >= from && x.InvoiceDate < through)
            .ToListAsync(cancellationToken);
        var purchases = await db.Purchases.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.PurchaseDate >= from && x.PurchaseDate < through)
            .ToListAsync(cancellationToken);
        var expenses = await db.Expenses.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.ExpenseDate >= from && x.ExpenseDate < through)
            .ToListAsync(cancellationToken);
        var salesReturns = await db.SalesReturns.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.ReturnDate >= from && x.ReturnDate < through)
            .ToListAsync(cancellationToken);
        var purchaseReturns = await db.PurchaseReturns.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.ReturnDate >= from && x.ReturnDate < through)
            .ToListAsync(cancellationToken);
        var payments = await db.Payments.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.PaymentDate >= from && x.PaymentDate < through)
            .ToListAsync(cancellationToken);

        var activeInvoices = invoices.Where(x => x.Status != (int)InvoiceStatus.Cancelled).ToList();
        var activePurchases = purchases.Where(x => x.Status != (int)PurchaseStatus.Cancelled).ToList();
        var completedSalesReturns = salesReturns.Where(x => x.Status == (int)ReturnStatus.Completed).Sum(x => x.RefundAmount);
        var completedPurchaseReturns = purchaseReturns.Where(x => x.Status == (int)ReturnStatus.Completed).Sum(x => x.RefundAmount);
        var completedPayments = payments.Where(x => x.Status == (int)PaymentStatus.Completed).ToList();

        var customers = db.Customers.AsNoTracking().Where(x => x.CompanyId == companyId);
        var products = db.Products.AsNoTracking().Where(x => x.CompanyId == companyId);

        return (new FinancialReportSummary
        {
            GrossSales = activeInvoices.Sum(InvoiceTotal),
            SalesReturns = completedSalesReturns,
            GrossPurchases = activePurchases.Sum(PurchaseTotal),
            PurchaseReturns = completedPurchaseReturns,
            OperatingExpenses = expenses.Where(x => x.Status == (int)ExpenseStatus.Approved || x.Status == (int)ExpenseStatus.Paid).Sum(x => x.Amount),
            OutstandingReceivables = activeInvoices.Sum(x => Math.Max(0, InvoiceTotal(x) - x.PaidAmount)),
            OutstandingPayables = activePurchases.Sum(x => Math.Max(0, PurchaseTotal(x) - x.PaidAmount)),
            PaymentsReceived = completedPayments.Where(x => !x.IsSupplier).Sum(x => x.Amount),
            PaymentsPaid = completedPayments.Where(x => x.IsSupplier).Sum(x => x.Amount),
            InvoiceCount = activeInvoices.Count,
            PurchaseCount = activePurchases.Count,
            CustomerCount = await customers.CountAsync(cancellationToken),
            ActiveCustomerCount = await customers.CountAsync(x => x.Status == 0, cancellationToken),
            ProductCount = await products.CountAsync(cancellationToken),
            LowStockCount = await products.CountAsync(x => x.Status == 0 && x.Stock <= x.MinimumStock, cancellationToken),
            SupplierCount = await db.Suppliers.CountAsync(x => x.CompanyId == companyId && x.IsActive, cancellationToken),
            OverdueInvoiceCount = activeInvoices.Count(x => x.Status == (int)InvoiceStatus.Overdue || x.DueDate.Date < DateTime.Today && InvoiceTotal(x) > x.PaidAmount)
        }, null);
    }

    private static decimal InvoiceTotal(LocalInvoiceEntity row)
    {
        var items = JsonSerializer.Deserialize<List<InvoiceLineItem>>(row.ItemsJson) ?? [];
        var subtotal = items.Sum(x => x.LineTotal);
        return subtotal + items.Sum(x => x.TaxAmount) + subtotal * row.TaxPct / 100 - subtotal * row.DiscountPct / 100;
    }

    private static decimal PurchaseTotal(LocalPurchaseEntity row)
        => (JsonSerializer.Deserialize<List<PurchaseLineItem>>(row.ItemsJson) ?? []).Sum(x => x.GrandTotal);
}
