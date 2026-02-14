using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Accounting.Common.Models;
using System.Globalization;

namespace Accounting.Common.Services;

public interface IAccountingService
{
    IEnumerable<AccountingEntry> ExplodePayload(string payload, string batchId);
}

public class AccountingService : IAccountingService
{
    public IEnumerable<AccountingEntry> ExplodePayload(string payload, string batchId)
    {
        if (string.IsNullOrWhiteSpace(payload)) return Enumerable.Empty<AccountingEntry>();

        try
        {
            // Try parse as a well-known "invoice" type first
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("type", out var t) && t.GetString() == "invoice")
            {
                return ExplodeInvoice(root, batchId);
            }

            // Otherwise try a simple array of entries [{account,amount,desc}, ...]
            if (root.ValueKind == JsonValueKind.Array)
            {
                var list = new List<AccountingEntry>();
                foreach (var it in root.EnumerateArray())
                {
                    var account = it.GetProperty("account").GetString() ?? "UNKNOWN";
                    var amount = decimal.Parse(it.GetProperty("amount").GetRawText(), CultureInfo.InvariantCulture);
                    var desc = it.TryGetProperty("desc", out var d) ? d.GetString() ?? "" : "";
                    list.Add(new AccountingEntry
                    {
                        EntryId = Guid.NewGuid().ToString(),
                        Account = account,
                        Amount = amount,
                        Description = desc,
                        BatchId = batchId
                    });
                }
                return list;
            }
        }
        catch
        {
            // fallback: parsing error — return empty
        }

        return Enumerable.Empty<AccountingEntry>();
    }

    private IEnumerable<AccountingEntry> ExplodeInvoice(JsonElement invoice, string batchId)
    {
        // Minimal invoice explosion:
        // - Debit: customerAccount = total
        // - Credits: each line -> account, amount
        // - Optionally: tax lines already supplied in lines or computed via taxRate
        var date = invoice.TryGetProperty("date", out var d) ? d.GetString() ?? DateTime.UtcNow.ToString("o") : DateTime.UtcNow.ToString("o");
        var invoiceId = invoice.TryGetProperty("invoiceId", out var id) ? id.GetString() ?? "" : "";
        var customerAccount = invoice.GetProperty("customerAccount").GetString() ?? "1100-AccountsReceivable";
        decimal total = invoice.TryGetProperty("total", out var tot) ? decimal.Parse(tot.GetRawText(), CultureInfo.InvariantCulture) : 0m;

        var entries = new List<AccountingEntry>();

        // Debit to customer
        entries.Add(new AccountingEntry
        {
            EntryId = Guid.NewGuid().ToString(),
            Account = customerAccount,
            Amount = total,
            Description = $"Invoice {invoiceId}",
            BatchId = batchId,
            Date = DateTime.Parse(date, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        });

        // Credits from lines
        if (invoice.TryGetProperty("lines", out var lines) && lines.ValueKind == JsonValueKind.Array)
        {
            foreach (var line in lines.EnumerateArray())
            {
                var acc = line.GetProperty("account").GetString() ?? "4000-Revenue";
                var amt = decimal.Parse(line.GetProperty("amount").GetRawText(), CultureInfo.InvariantCulture);
                var desc = line.TryGetProperty("desc", out var ld) ? ld.GetString() ?? "" : "";
                entries.Add(new AccountingEntry
                {
                    EntryId = Guid.NewGuid().ToString(),
                    Account = acc,
                    Amount = amt,
                    Description = desc != "" ? desc : $"Line of {invoiceId}",
                    BatchId = batchId,
                    Date = DateTime.Parse(date, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                });
            }
        }

        // If total differs from sum of lines, compute a tax/adjustment entry to balance
        var sumLines = entries.Where(e => e.Account != customerAccount).Sum(e => e.Amount);
        var taxAmount = decimal.Round(total - sumLines, 2);
        if (taxAmount != 0)
        {
            // prefer provided taxAccount, fallback to default
            var taxAccount = invoice.TryGetProperty("taxAccount", out var ta) ? ta.GetString() ?? "2100-Tax" : "2100-Tax";
            entries.Add(new AccountingEntry
            {
                EntryId = Guid.NewGuid().ToString(),
                Account = taxAccount,
                Amount = taxAmount,
                Description = $"Tax/Adjustment for {invoiceId}",
                BatchId = batchId,
                Date = DateTime.Parse(date, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            });
        }

        // Ensure debits == credits: sum of non-customer accounts should equal customer debit
        var debit = entries.Where(e => e.Account == customerAccount).Sum(e => e.Amount);
        var credits = entries.Where(e => e.Account != customerAccount).Sum(e => e.Amount);

        // Simple rounding fix: if difference within 1 cent, adjust first credit
        var diff = debit - credits;
        if (diff != 0)
        {
            if (Math.Abs(diff) <= 0.01m && entries.Count > 1)
            {
                // adjust the first credit entry
                var firstCredit = entries.FirstOrDefault(e => e.Account != customerAccount);
                if (firstCredit != null)
                {
                    firstCredit = firstCredit with { Amount = firstCredit.Amount + diff };
                    // replace
                    var idx = entries.FindIndex(e => e.EntryId == firstCredit.EntryId);
                    if (idx >= 0) entries[idx] = firstCredit;
                }
            }
            else
            {
                // If imbalance larger than rounding tolerance, throw to surface error
                throw new InvalidOperationException($"Invoice {invoiceId} is unbalanced (debit {debit} != credit {credits}).");
            }
        }

        return entries;
    }
}

