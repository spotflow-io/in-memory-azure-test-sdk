using System.Diagnostics.CodeAnalysis;

using Spotflow.InMemory.Azure.Storage.Resources;

namespace Spotflow.InMemory.Azure.Storage.Tables.Internals;

internal class InMemoryTableService
{
    private readonly Dictionary<string, InMemoryTable> _tables = new();

    public InMemoryTableService(InMemoryStorageAccount account)
    {
        Uri = CreateServiceUriFromAccountName(account.Name, account.Provider);
        Account = account;
    }

    public Uri Uri { get; }
    public InMemoryStorageAccount Account { get; }

    public bool TryAddTable(string tableName, out InMemoryTable result)
    {
        lock (_tables)
        {
            if (_tables.TryGetValue(tableName, out var existingTable))
            {
                result = existingTable;
                return false;
            }

            var newTable = new InMemoryTable(tableName, this);
            _tables.Add(tableName, newTable);

            result = newTable;
            return true;
        }
    }

    public bool TryGetTable(string tableName, [NotNullWhen(true)] out InMemoryTable? result)
    {
        lock (_tables)
        {
            return _tables.TryGetValue(tableName, out result);
        }
    }

    public bool TryDeleteTable(string tableName)
    {
        lock (_tables)
        {
            return _tables.Remove(tableName);
        }
    }

    public override string ToString() => Uri.ToString().TrimEnd('/');

    public static Uri CreateServiceUriFromAccountName(string accountName, InMemoryStorageProvider provider)
    {
        return new($"https://{accountName}.table.{provider.HostnameSuffix}");
    }

    public Uri CreateTableSasUri(string tableName) => TableUriUtils.UriForTable(Uri, tableName);
}
