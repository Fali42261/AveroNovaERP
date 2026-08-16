using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockPaymentService : IPaymentService
{
    private int _counter = 3;

    public Task<List<PaymentModel>> GetAllAsync(Guid companyId)
        => Task.FromResult(MockDataStore.Payments.Where(p => p.CompanyId == companyId).ToList());

    public Task<PaymentModel?> GetByIdAsync(Guid id)
        => Task.FromResult(MockDataStore.Payments.FirstOrDefault(p => p.LocalId == id));

    public Task<(bool Ok, string? Error)> CreateAsync(PaymentModel payment)
    {
        payment.LocalId    = Guid.NewGuid();
        payment.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Payments.Add(payment);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> UpdateAsync(PaymentModel payment)
    {
        var idx = MockDataStore.Payments.FindIndex(p => p.LocalId == payment.LocalId);
        if (idx < 0) return Task.FromResult((false, "Payment not found."));
        payment.SyncStatus = SyncStatus.PendingSync;
        MockDataStore.Payments[idx] = payment;
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        var item = MockDataStore.Payments.FirstOrDefault(p => p.LocalId == id);
        if (item == null) return Task.FromResult((false, "Payment not found."));
        MockDataStore.Payments.Remove(item);
        return Task.FromResult<(bool, string?)>((true, null));
    }

    public Task<string> GetNextPaymentNumberAsync(Guid companyId)
        => Task.FromResult($"PAY-2026-{_counter++:D3}");
}
