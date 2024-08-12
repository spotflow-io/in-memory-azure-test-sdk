using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using Azure.Data.Tables.Sas;

using Spotflow.InMemory.Azure.Internals;
using Spotflow.InMemory.Azure.Storage.Resources;
using Spotflow.InMemory.Azure.Storage.Tables.Hooks;
using Spotflow.InMemory.Azure.Storage.Tables.Hooks.Contexts;
using Spotflow.InMemory.Azure.Storage.Tables.Internals;

using static Spotflow.InMemory.Azure.Storage.Tables.Internals.InMemoryTable;

namespace Spotflow.InMemory.Azure.Storage.Tables;

public class InMemoryTableClient : TableClient
{
    private const int _defaultMaxPerPage = 1000;

    private readonly TableScope _scope;

    #region Constructors

    public InMemoryTableClient(string connectionString, string tableName, InMemoryStorageProvider provider) : this(connectionString, null, tableName, provider) { }

    public InMemoryTableClient(Uri tableServiceUri, string tableName, InMemoryStorageProvider provider) : this(null, tableServiceUri, tableName, provider) { }

    public InMemoryTableClient(Uri tableUri, InMemoryStorageProvider provider) : this(null, tableUri, null, provider) { }

    private InMemoryTableClient(string? connectionString, Uri? tableUri, string? tableName, InMemoryStorageProvider provider)
    {
        var builder = TableUriUtils.BuilderForTable(connectionString, tableUri, tableName, provider);
        Uri = builder.ToUri();
        AccountName = builder.AccountName;
        Name = builder.Tablename;
        Provider = provider;

        _scope = new(builder.AccountName, builder.Tablename);
    }

    public static InMemoryTableClient FromAccount(InMemoryStorageAccount account, string tableName)
    {
        return new(account.TableServiceUri, tableName, account.Provider);
    }

    #endregion

    public override Uri Uri { get; }
    public override string Name { get; }
    public override string AccountName { get; }

    public InMemoryStorageProvider Provider { get; }

    #region Create

    public override Response<TableItem> CreateIfNotExists(CancellationToken cancellationToken = default)
    {
        (var item, var added) = CreateIfNotExistsCore();

        return added switch
        {
            true => InMemoryResponse.FromValue(item, 201),
            false => InMemoryResponse.FromValue(item, 409)
        };

    }

    public override async Task<Response<TableItem>> CreateIfNotExistsAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        return CreateIfNotExists(cancellationToken);
    }

    public override Response<TableItem> Create(CancellationToken cancellationToken = default)
    {
        var item = CreateAsync(cancellationToken).EnsureCompleted();

        return InMemoryResponse.FromValue(item, 201);
    }

    public override async Task<Response<TableItem>> CreateAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var beforeContext = new TableCreateBeforeHookContext(_scope, Provider, cancellationToken);

        await ExecuteBeforeHooksAsync(beforeContext).ConfigureAwait(false);

        var service = GetService();

        if (!service.TryAddTable(Name, out var table))
        {
            throw TableExceptionFactory.TableAlreadyExists(AccountName, Name);
        }

        var afterContext = new TableCreateAfterHookContext(beforeContext);

        await ExecuteAfterHooksAsync(afterContext).ConfigureAwait(false);

        var tableItem = table.AsItem();

        return InMemoryResponse.FromValue(tableItem, 201);
    }

    private (TableItem, bool) CreateIfNotExistsCore()
    {
        var service = GetService();

        var added = service.TryAddTable(Name, out var table);

        return (table.AsItem(), added);
    }


    #endregion

    #region Query

    public override Pageable<T> Query<T>(Expression<Func<T, bool>> filter, int? maxPerPage = null, IEnumerable<string>? select = null, CancellationToken cancellationToken = default)
    {
        var filterCompiled = filter.Compile();

        var entities = QueryCoreAsync(filterCompiled, cancellationToken).EnsureCompleted();

        return new InMemoryPageable.Sync<T>(entities, maxPerPage ?? _defaultMaxPerPage);
    }

    public override AsyncPageable<T> QueryAsync<T>(Expression<Func<T, bool>> filter, int? maxPerPage = null, IEnumerable<string>? select = null, CancellationToken cancellationToken = default)
    {
        var filterCompiled = filter.Compile();

        var entities = QueryCoreAsync(filterCompiled, cancellationToken).EnsureCompleted();

        return new InMemoryPageable.YieldingAsync<T>(entities, maxPerPage ?? _defaultMaxPerPage);
    }

    public override Pageable<T> Query<T>(string? filter = null, int? maxPerPage = null, IEnumerable<string>? select = null, CancellationToken cancellationToken = default)
    {
        var matcher = new TextQueryFilterMatcher(filter, Provider.LoggerFactory);

        var entities = QueryCoreAsync<T>(matcher.IsMatch, cancellationToken).EnsureCompleted();

        return new InMemoryPageable.Sync<T>(entities, maxPerPage ?? _defaultMaxPerPage);
    }


    public override AsyncPageable<T> QueryAsync<T>(string? filter = null, int? maxPerPage = null, IEnumerable<string>? select = null, CancellationToken cancellationToken = default)
    {
        var matcher = new TextQueryFilterMatcher(filter, Provider.LoggerFactory);

        var entities = QueryCoreAsync<T>(matcher.IsMatch, cancellationToken).EnsureCompleted();

        return new InMemoryPageable.YieldingAsync<T>(entities, maxPerPage ?? _defaultMaxPerPage);
    }

    private Task<IReadOnlyList<T>> QueryCoreAsync<T>(Func<T, bool> typedFilter, CancellationToken cancellationToken) where T : class, ITableEntity
    {
        return QueryCoreAsync(entities => entities.Select(e => e.ToAzureTableEntity<T>()).Where(typedFilter), cancellationToken);
    }

    private Task<IReadOnlyList<T>> QueryCoreAsync<T>(Func<InMemoryTableEntity, bool> genericFilter, CancellationToken cancellationToken) where T : class, ITableEntity
    {
        return QueryCoreAsync(e => e.Where(genericFilter).Select(e => e.ToAzureTableEntity<T>()), cancellationToken);
    }

    private async Task<IReadOnlyList<T>> QueryCoreAsync<T>(Func<IEnumerable<InMemoryTableEntity>, IEnumerable<T>> filter, CancellationToken cancellationToken) where T : class, ITableEntity
    {
        await Task.Yield();

        var beforeContext = new TableQueryBeforeHookContext(_scope, Provider, cancellationToken);

        await ExecuteBeforeHooksAsync(beforeContext).ConfigureAwait(false);

        var table = GetTable();

        var entities = table.GetEntities(filter);

        var afterContext = new TableQueryAfterHookContext(beforeContext)
        {
            Entities = entities
        };

        await ExecuteAfterHooksAsync(afterContext).ConfigureAwait(false);

        return entities;
    }

    #endregion

    #region Upsert & Update Entity

    public override Response UpsertEntity<T>(T entity, TableUpdateMode mode = TableUpdateMode.Merge, CancellationToken cancellationToken = default)
    {
        return UpsertCoreAsync(entity, ETag.All, mode, cancellationToken).EnsureCompleted();
    }

    public override async Task<Response> UpsertEntityAsync<T>(T entity, TableUpdateMode mode = TableUpdateMode.Merge, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        return await UpsertCoreAsync(entity, ETag.All, mode, cancellationToken);
    }

    public override Response UpdateEntity<T>(T entity, ETag ifMatch, TableUpdateMode mode = TableUpdateMode.Merge, CancellationToken cancellationToken = default)
    {
        return UpdateCore(entity, ifMatch, mode);
    }

    public override async Task<Response> UpdateEntityAsync<T>(T entity, ETag ifMatch, TableUpdateMode mode = TableUpdateMode.Merge, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        return UpdateCore(entity, ifMatch, mode);
    }

    private InMemoryResponse UpdateCore<T>(T entity, ETag ifMatch, TableUpdateMode mode) where T : ITableEntity
    {
        if (ifMatch.IsEmpty())
        {
            ifMatch = ETag.All;
        }

        var table = GetTable();

        if (!table.TryUpsertEntity(entity, ifMatch, mode, mustExist: true, out var eTag, out var error))
        {
            throw error.GetClientException();
        }

        return new(204, eTag: eTag.Value);
    }

    private async Task<InMemoryResponse> UpsertCoreAsync<T>(T entity, ETag ifMatch, TableUpdateMode mode, CancellationToken cancellationToken) where T : ITableEntity
    {
        var beforeContext = new EntityUpsertBeforeHookContext(_scope.ForEntity(entity), Provider, cancellationToken)
        {
            Entity = entity
        };

        await ExecuteBeforeHooksAsync(beforeContext).ConfigureAwait(false);

        if (ifMatch.IsEmpty())
        {
            ifMatch = ETag.All;
        }

        var table = GetTable();

        if (!table.TryUpsertEntity(entity, ifMatch, mode, mustExist: false, out var eTag, out var error))
        {
            throw error.GetClientException();
        }

        var afterContext = new EntityUpsertAfterHookContext(beforeContext)
        {
            Entity = entity,
        };

        await ExecuteAfterHooksAsync(afterContext).ConfigureAwait(false);

        return new(204, eTag: eTag.Value);
    }

    #endregion

    #region Delete Entity

    public override Response DeleteEntity(string partitionKey, string rowKey, ETag ifMatch = default, CancellationToken cancellationToken = default)
    {
        var table = GetTable();

        if (!table.TryDeleteEntity(partitionKey, rowKey, ifMatch, out var error))
        {
            if (error is EntityDeleteError.NotFound)
            {
                return new InMemoryResponse(404);
            }

            throw error.GetClientException();
        }

        return new InMemoryResponse(204);

    }

    public override async Task<Response> DeleteEntityAsync(string partitionKey, string rowKey, ETag ifMatch = default, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        return DeleteEntity(partitionKey, rowKey, ifMatch, cancellationToken);
    }

    #endregion

    #region Add Entity

    public override Response AddEntity<T>(T entity, CancellationToken cancellationToken = default)
    {
        return AddEntityCoreAsync(entity, cancellationToken).EnsureCompleted();
    }

    public override async Task<Response> AddEntityAsync<T>(T entity, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        return await AddEntityCoreAsync(entity, cancellationToken);
    }

    private async Task<Response> AddEntityCoreAsync<T>(T entity, CancellationToken cancellationToken) where T : ITableEntity
    {
        var beforeContext = new EntityAddBeforeHookContext(_scope.ForEntity(entity), Provider, cancellationToken)
        {
            Entity = entity
        };

        await ExecuteBeforeHooksAsync(beforeContext).ConfigureAwait(false);

        var table = GetTable();

        if (!table.TryAddEntity(entity, out var eTag, out var error))
        {
            throw error.GetClientException();
        }

        var response = new InMemoryResponse(204, eTag: eTag.Value);

        var afterContext = new EntityAddAfterHookContext(beforeContext)
        {
            Entity = entity
        };

        await ExecuteAfterHooksAsync(afterContext).ConfigureAwait(false);

        return response;
    }

    #endregion

    #region Table Exists

    public bool Exists() => TryGetTable(out _, out _);

    #endregion

    #region Get Entity

    public override Response<T> GetEntity<T>(string partitionKey, string rowKey, IEnumerable<string>? select = null, CancellationToken cancellationToken = default)
    {
        return GetEntityAsync<T>(partitionKey, rowKey, select, cancellationToken).EnsureCompleted();
    }

    public override async Task<Response<T>> GetEntityAsync<T>(string partitionKey, string rowKey, IEnumerable<string>? select = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var entity = await FindEntityCoreAsync<T>(partitionKey, rowKey, cancellationToken);

        if (entity is null)
        {
            throw TableExceptionFactory.EntityNotFound(AccountName, Name, partitionKey, rowKey);
        }

        return InMemoryResponse.FromValue(entity, 200, eTag: entity.ETag);
    }

    public override NullableResponse<T> GetEntityIfExists<T>(string partitionKey, string rowKey, IEnumerable<string>? select = null, CancellationToken cancellationToken = default)
    {
        return GetEntityIfExistsAsync<T>(partitionKey, rowKey, select, cancellationToken).EnsureCompleted();
    }

    public override async Task<NullableResponse<T>> GetEntityIfExistsAsync<T>(string partitionKey, string rowKey, IEnumerable<string>? select = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var entity = await FindEntityCoreAsync<T>(partitionKey, rowKey, cancellationToken);

        if (entity is null)
        {
            return InMemoryNullableResponse<T>.FromNull();
        }

        return InMemoryNullableResponse<T>.FromValue(entity);
    }

    private async Task<T?> FindEntityCoreAsync<T>(string partitionKey, string rowKey, CancellationToken cancellationToken) where T : class, ITableEntity
    {
        var table = GetTable();

        var entities = await QueryCoreAsync<T>(typedFilter: entity => filter(entity, partitionKey, rowKey), cancellationToken);

        if (entities.Count == 0)
        {
            return null;
        }

        if (entities.Count == 1)
        {
            return entities[0];

        }

        throw new InvalidOperationException("Multiple entities returned to point query.");

        static bool filter(T entity, string partitionKey, string rowKey)
        {
            if (!entity.PartitionKey.Equals(partitionKey, StringComparison.Ordinal))
            {
                return false;
            }

            if (!entity.RowKey.Equals(rowKey, StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }
    }

    #endregion

    #region Transaction

    public override Response<IReadOnlyList<Response>> SubmitTransaction(IEnumerable<TableTransactionAction> transactionActions, CancellationToken cancellationToken = default)
    {
        var table = GetTable();

        var transactions = transactionActions.ToList();

        if (!table.TrySubmitTransaction(transactions, out var results, out var error))
        {
            throw error.GetClientException();
        }

        if (results.Count != transactions.Count)
        {
            throw new InvalidOperationException("Transaction results count does not match transaction actions count.");
        }

        var responses = new List<InMemoryResponse>();

        for (var i = 0; i < results.Count; i++)
        {
            var t = transactions[i];
            var r = results[i];

            var status = t.ActionType switch
            {
                TableTransactionActionType.Add => 204,
                TableTransactionActionType.UpdateMerge => 204,
                TableTransactionActionType.UpdateReplace => 204,
                TableTransactionActionType.Delete => 204,
                TableTransactionActionType.UpsertMerge => 204,
                TableTransactionActionType.UpsertReplace => 204,
            };

            responses.Add(new(status, r.ETag));
        }

        return InMemoryResponse.FromValue<IReadOnlyList<Response>>(responses, 202);

    }

    public override async Task<Response<IReadOnlyList<Response>>> SubmitTransactionAsync(IEnumerable<TableTransactionAction> transactionActions, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        return SubmitTransaction(transactionActions, cancellationToken);
    }

    #endregion

    #region SAS

    public override TableSasBuilder GetSasBuilder(TableSasPermissions permissions, DateTimeOffset expiresOn) => new(Name, permissions, expiresOn);

    public override TableSasBuilder GetSasBuilder(string rawPermissions, DateTimeOffset expiresOn) => new(Name, rawPermissions, expiresOn);

    public override Uri GenerateSasUri(TableSasPermissions permissions, DateTimeOffset expiresOn) => Uri;

    public override Uri GenerateSasUri(TableSasBuilder builder)
    {
        if (builder.TableName != Name)
        {
            throw new InvalidOperationException($"Table name in the builder ({builder.TableName}) does not match actual table name ({Name}).");
        }

        return Uri;
    }

    #endregion

    #region Delete Table

    public override Response Delete(CancellationToken cancellationToken = default)
    {
        var service = GetService();

        if (!service.TryDeleteTable(Name))
        {
            throw TableExceptionFactory.TableNotFound(Name, service);
        }

        return new InMemoryResponse(204);
    }

    public override async Task<Response> DeleteAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        return Delete(cancellationToken);
    }

    #endregion

    private InMemoryTableService GetService()
    {
        if (!Provider.TryGetAccount(AccountName, out var account))
        {
            throw TableExceptionFactory.TableServiceNotFound(AccountName, Provider);
        }

        return account.TableService;
    }

    private InMemoryTable GetTable()
    {
        if (!TryGetTable(out var service, out var table))
        {
            throw TableExceptionFactory.TableNotFound(Name, service);
        }

        return table;
    }

    private bool TryGetTable(out InMemoryTableService service, [NotNullWhen(true)] out InMemoryTable? table)
    {
        service = GetService();

        if (!service.TryGetTable(Name, out table))
        {
            return false;
        }

        return true;
    }

    private Task ExecuteBeforeHooksAsync<TContext>(TContext context) where TContext : TableServiceBeforeHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }

    private Task ExecuteAfterHooksAsync<TContext>(TContext context) where TContext : TableServiceAfterHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }


    #region Unsupported

    public override Task<Response<IReadOnlyList<TableSignedIdentifier>>> GetAccessPoliciesAsync(CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Response<IReadOnlyList<TableSignedIdentifier>> GetAccessPolicies(CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> SetAccessPolicyAsync(IEnumerable<TableSignedIdentifier> tableAcl, CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    public override Response SetAccessPolicy(IEnumerable<TableSignedIdentifier> tableAcl, CancellationToken cancellationToken = default)
    {
        throw TableExceptionFactory.MethodNotSupported();
    }

    #endregion

}
