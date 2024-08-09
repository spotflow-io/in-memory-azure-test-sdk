using System.Linq.Expressions;

using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using Azure.Data.Tables.Sas;

using Spotflow.InMemory.Azure.Storage.Resources;
using Spotflow.InMemory.Azure.Storage.Tables.Internals;

namespace Spotflow.InMemory.Azure.Storage.Tables;

public class InMemoryTableServiceClient : TableServiceClient
{
    #region Constructors

    public InMemoryTableServiceClient(string connectionString, InMemoryStorageProvider provider) : this(connectionString, null, provider) { }

    public InMemoryTableServiceClient(Uri tableServiceUri, InMemoryStorageProvider provider) : this(null, tableServiceUri, provider) { }

    private InMemoryTableServiceClient(string? connectionString, Uri? uri, InMemoryStorageProvider provider)
    {
        var builder = TableUriUtils.BuilderForService(connectionString, uri, provider);

        Uri = builder.ToUri();
        Provider = provider;
        AccountName = builder.AccountName;
    }

    public static InMemoryTableServiceClient FromAccount(InMemoryStorageAccount account)
    {
        return new(account.TableServiceUri, account.Provider);
    }

    #endregion

    public override Uri Uri { get; }
    public override string AccountName { get; }
    public InMemoryStorageProvider Provider { get; }

    public override TableClient GetTableClient(string tableName)
    {
        var uriBuilder = new TableUriBuilder(Uri)
        {
            Tablename = tableName
        };

        return new InMemoryTableClient(uriBuilder.ToUri(), Provider);
    }

    #region Unsupported

    public override TableAccountSasBuilder GetSasBuilder(TableAccountSasPermissions permissions, TableAccountSasResourceTypes resourceTypes, DateTimeOffset expiresOn)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override TableAccountSasBuilder GetSasBuilder(string rawPermissions, TableAccountSasResourceTypes resourceTypes, DateTimeOffset expiresOn)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override AsyncPageable<TableItem> QueryAsync(string? filter = null, int? maxPerPage = null, CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Pageable<TableItem> Query(string? filter = null, int? maxPerPage = null, CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override AsyncPageable<TableItem> QueryAsync(FormattableString filter, int? maxPerPage = null, CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Pageable<TableItem> Query(FormattableString filter, int? maxPerPage = null, CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override AsyncPageable<TableItem> QueryAsync(Expression<Func<TableItem, bool>> filter, int? maxPerPage = null, CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Pageable<TableItem> Query(Expression<Func<TableItem, bool>> filter, int? maxPerPage = null, CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Response<TableItem> CreateTable(string tableName, CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<TableItem>> CreateTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Response<TableItem> CreateTableIfNotExists(string tableName, CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<TableItem>> CreateTableIfNotExistsAsync(string tableName, CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Response DeleteTable(string tableName, CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> DeleteTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Response SetProperties(TableServiceProperties properties, CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> SetPropertiesAsync(TableServiceProperties properties, CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Response<TableServiceProperties> GetProperties(CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<TableServiceProperties>> GetPropertiesAsync(CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<TableServiceStatistics>> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Response<TableServiceStatistics> GetStatistics(CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Uri GenerateSasUri(TableAccountSasPermissions permissions, TableAccountSasResourceTypes resourceTypes, DateTimeOffset expiresOn)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Uri GenerateSasUri(TableAccountSasBuilder builder)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    #endregion
}
