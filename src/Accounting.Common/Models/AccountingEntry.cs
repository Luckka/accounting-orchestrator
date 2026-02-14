using System;

namespace Accounting.Common.Models;

public record AccountingEntry
{
    public string EntryId { get; init; } = Guid.NewGuid().ToString();
    public string Account { get; init; } = default!;
    public decimal Amount { get; init; }
    public DateTime Date { get; init; } = DateTime.UtcNow;
    public string Description { get; init; } = default!;
    public string BatchId { get; init; } = default!;
}

