using System.Linq;
using Accounting.Common.Services;
using Xunit;

namespace Accounting.Tests;

public class AccountingServiceTests
{
    [Fact]
    public void ExplodePayload_ArrayOfEntries_ReturnsEntries()
    {
        var svc = new AccountingService();
        var payload = "[{\"account\":\"4000-Revenue\",\"amount\":1000.00,\"desc\":\"Sale\"},{\"account\":\"2100-Tax\",\"amount\":180.00,\"desc\":\"Tax\"}]";
        var entries = svc.ExplodePayload(payload, "B-TEST").ToList();
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Account == "4000-Revenue" && e.Amount == 1000.00m);
        Assert.Contains(entries, e => e.Account == "2100-Tax" && e.Amount == 180.00m);
    }

    [Fact]
    public void ExplodePayload_Invoice_ReturnsBalancedEntries()
    {
        var svc = new AccountingService();
        var payload = "{\"type\":\"invoice\",\"invoiceId\":\"INV-1\",\"date\":\"2026-02-12\",\"customerAccount\":\"1100-AR\",\"total\":1180.00,\"lines\":[{\"account\":\"4000-Revenue\",\"amount\":1000.00}],\"taxAccount\":\"2100-Tax\"}";
        var entries = svc.ExplodePayload(payload, "B-INV").ToList();
        Assert.True(entries.Count >= 2);
        var debit = entries.Where(e => e.Account == "1100-AR").Sum(e => e.Amount);
        var credits = entries.Where(e => e.Account != "1100-AR").Sum(e => e.Amount);
        Assert.Equal(debit, credits);
    }
}

