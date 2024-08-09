using Azure.Data.Tables;

using Spotflow.InMemory.Azure.Storage.Internals;

namespace Spotflow.InMemory.Azure.Storage.Tables.Internals;

internal static class TableUriUtils
{
    public static TableUriBuilder BuilderForService(string? connectionString, Uri? uri, InMemoryStorageProvider provider)
    {
        return Builder(connectionString, uri, null, provider);
    }

    public static TableUriBuilder BuilderForTable(string? connectionString, Uri? uri, string? tableName, InMemoryStorageProvider provider)
    {
        var builder = Builder(connectionString, uri, tableName, provider);

        if (string.IsNullOrWhiteSpace(builder.Tablename))
        {
            throw new InvalidOperationException("Table name must be specified when creating a table client.");
        }

        return builder;
    }

    public static Uri UriForTable(Uri serviceUri, string tableName)
    {
        var builder = Builder(null, serviceUri, tableName, null);
        return builder.ToUri();
    }

    private static TableUriBuilder Builder(string? connectionString, Uri? uri, string? tableName, InMemoryStorageProvider? provider)
    {
        if (connectionString is not null && uri is not null)
        {
            throw new InvalidOperationException("Both a connection string and a URI cannot be provided.");
        }

        if (uri is null)
        {
            if (connectionString is null)
            {
                throw new InvalidOperationException("Either a connection string or a URI must be provided.");
            }

            if (provider is null)
            {
                throw new InvalidOperationException("A provider must be provided when using a connection string.");
            }

            var accountName = StorageConnectionStringUtils.GetAccountNameFromConnectionString(connectionString);
            uri = InMemoryTableService.CreateServiceUriFromAccountName(accountName, provider);
        }

        var builder = new TableUriBuilder(uri);

        if (tableName is not null)
        {
            builder.Tablename = tableName;
        }

        return builder;
    }

}

