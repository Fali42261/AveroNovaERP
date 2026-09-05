using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using Xunit;

namespace AveroNova.API.Tests;

public sealed class PaymentDomainTests
{
    [Fact]
    public void ApplyUpdate_UpdatesFieldsAndMarksPending()
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            PaymentNumber = "OLD",
            PartyName = "Old Party",
            Amount = 10m,
            SyncStatus = RecordSyncStatus.Synced,
            SyncVersion = 1
        };

        var partyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var paymentDate = new DateTime(2026, 9, 5);

        payment.ApplyUpdate(
            " PAY-2026-0001 ",
            partyId,
            " Customer One ",
            false,
            invoiceId,
            " INV-2026-0001 ",
            250.50m,
            0,
            paymentDate,
            " REF-001 ",
            " First payment ",
            1);

        Assert.Equal("PAY-2026-0001", payment.PaymentNumber);
        Assert.Equal(partyId, payment.PartyId);
        Assert.Equal("Customer One", payment.PartyName);
        Assert.False(payment.IsSupplier);
        Assert.Equal(invoiceId, payment.InvoiceId);
        Assert.Equal("INV-2026-0001", payment.InvoiceNumber);
        Assert.Equal(250.50m, payment.Amount);
        Assert.Equal(0, payment.Method);
        Assert.Equal(paymentDate, payment.PaymentDate);
        Assert.Equal("REF-001", payment.Reference);
        Assert.Equal("First payment", payment.Notes);
        Assert.Equal(1, payment.Status);
        Assert.Equal(RecordSyncStatus.Pending, payment.SyncStatus);
        Assert.True(payment.SyncVersion > 1);
    }

    [Fact]
    public void ApplyUpdate_NullReferenceAndNotes_BecomeEmptyStrings()
    {
        var payment = new Payment { SyncStatus = RecordSyncStatus.Synced };

        payment.ApplyUpdate(
            "PAY-1",
            Guid.NewGuid(),
            "Party",
            false,
            null,
            string.Empty,
            1m,
            5,
            DateTime.UtcNow,
            null,
            null,
            1);

        Assert.Equal(string.Empty, payment.Reference);
        Assert.Equal(string.Empty, payment.Notes);
        Assert.Equal(RecordSyncStatus.Pending, payment.SyncStatus);
    }
}
