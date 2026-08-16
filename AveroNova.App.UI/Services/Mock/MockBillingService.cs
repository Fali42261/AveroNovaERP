using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockBillingService : IBillingService
{
    private int _counter = 5;

    public Task<List<InvoiceModel>> GetAllAsync(Guid companyId)
        => Task.FromResult(MockDataStore.Invoices.Where(i => i.CompanyId == companyId).ToList());

    public Task<InvoiceModel?> GetByIdAsync(Guid id)
        => Task.FromResult(MockDataStore.Invoices.FirstOrDefault(i => i.LocalId == id));

    public Task<(bool Ok, string? Error)> CreateAsync(InvoiceModel invoice)
    {
        invoice.LocalId    = Guid.NewGuid();
        invoice.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Invoices.Add(invoice);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> UpdateAsync(InvoiceModel invoice)
    {
        var idx = MockDataStore.Invoices.FindIndex(i => i.LocalId == invoice.LocalId);
        if (idx < 0) return Task.FromResult((false, "Invoice not found."));
        invoice.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Invoices[idx] = invoice;
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        var item = MockDataStore.Invoices.FirstOrDefault(i => i.LocalId == id);
        if (item == null) return Task.FromResult((false, "Invoice not found."));
        MockDataStore.Invoices.Remove(item);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> CancelAsync(Guid id)
    {
        var item = MockDataStore.Invoices.FirstOrDefault(i => i.LocalId == id);
        if (item == null) return Task.FromResult((false, "Invoice not found."));
        item.Status = InvoiceStatus.Cancelled;
        item.SyncStatus = SyncStatus.PendingSync;
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<string> GetNextInvoiceNumberAsync(Guid companyId)
        => Task.FromResult($"INV-2026-{_counter++:D3}");

    public Task<List<InvoiceModel>> GetByCustomerAsync(Guid customerId)
        => Task.FromResult(MockDataStore.Invoices.Where(i => i.CustomerId == customerId).ToList());

    public Task<List<InvoiceModel>> GetOverdueAsync(Guid companyId)
        => Task.FromResult(MockDataStore.Invoices
            .Where(i => i.CompanyId == companyId && i.Status == InvoiceStatus.Overdue).ToList());
}
