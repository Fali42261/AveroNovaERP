using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using Xunit;

namespace AveroNova.API.Tests;

public sealed class ReturnDomainTests
{
    [Fact]
    public void SalesReturn_ApplyUpdate_UpdatesFieldsAndMarksPending()
    {
        var row = new SalesReturn
        {
            CompanyId = Guid.NewGuid(),
            ReturnNumber = "SR-2026-0001",
            InvoiceId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Reason = "Old",
            Notes = "Old",
            RefundAmount = 10m,
            SyncVersion = 4,
            SyncStatus = RecordSyncStatus.Synced
        };
        var invoiceId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var source = new SalesReturn
        {
            InvoiceId = invoiceId,
            InvoiceNumber = "INV-100",
            CustomerId = customerId,
            CustomerName = "Customer",
            ReturnDate = new DateTime(2026, 9, 5),
            ItemsJson = "[{\"quantity\":1}]",
            Reason = " Damaged ",
            Notes = " Note ",
            RefundAmount = 99m,
            Status = 1
        };

        row.ApplyUpdate(source);

        Assert.Equal(invoiceId, row.InvoiceId);
        Assert.Equal(customerId, row.CustomerId);
        Assert.Equal("Damaged", row.Reason);
        Assert.Equal("Note", row.Notes);
        Assert.Equal(99m, row.RefundAmount);
        Assert.Equal(5, row.SyncVersion);
        Assert.Equal(RecordSyncStatus.Pending, row.SyncStatus);
        Assert.NotNull(row.UpdatedAt);
    }

    [Fact]
    public void SalesReturn_ApplyUpdate_NormalizesBlankItemsJson()
    {
        var row = new SalesReturn { Reason = "x", Notes = "x" };
        row.ApplyUpdate(new SalesReturn { Reason = "Reason", Notes = "", ItemsJson = "   " });
        Assert.Equal("[]", row.ItemsJson);
    }

    [Fact]
    public void PurchaseReturn_ApplyUpdate_UpdatesFieldsAndMarksPending()
    {
        var row = new PurchaseReturn
        {
            CompanyId = Guid.NewGuid(),
            ReturnNumber = "PR-2026-0001",
            PurchaseId = Guid.NewGuid(),
            SupplierId = Guid.NewGuid(),
            Reason = "Old",
            Notes = "Old",
            RefundAmount = 15m,
            SyncVersion = 2,
            SyncStatus = RecordSyncStatus.Synced
        };
        var purchaseId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var source = new PurchaseReturn
        {
            PurchaseId = purchaseId,
            PurchaseNumber = "PO-100",
            SupplierId = supplierId,
            SupplierName = "Supplier",
            ReturnDate = new DateTime(2026, 9, 5),
            ItemsJson = "[{\"quantity\":2}]",
            Reason = " Wrong item ",
            Notes = " Return note ",
            RefundAmount = 125m,
            Status = 2
        };

        row.ApplyUpdate(source);

        Assert.Equal(purchaseId, row.PurchaseId);
        Assert.Equal(supplierId, row.SupplierId);
        Assert.Equal("Wrong item", row.Reason);
        Assert.Equal("Return note", row.Notes);
        Assert.Equal(125m, row.RefundAmount);
        Assert.Equal(3, row.SyncVersion);
        Assert.Equal(RecordSyncStatus.Pending, row.SyncStatus);
        Assert.NotNull(row.UpdatedAt);
    }

    [Fact]
    public void PurchaseReturn_ApplyUpdate_NormalizesBlankItemsJson()
    {
        var row = new PurchaseReturn { Reason = "x", Notes = "x" };
        row.ApplyUpdate(new PurchaseReturn { Reason = "Reason", Notes = "", ItemsJson = null! });
        Assert.Equal("[]", row.ItemsJson);
    }
}
