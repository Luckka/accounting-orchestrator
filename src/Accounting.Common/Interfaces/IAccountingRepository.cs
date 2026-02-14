using System.Collections.Generic;
using System.Threading.Tasks;
using Accounting.Common.Models;

namespace Accounting.Common.Interfaces;

public interface IAccountingRepository
{
    Task SaveEntryAsync(AccountingEntry entry);
    Task SaveEntriesAsync(IEnumerable<AccountingEntry> entries);
}

