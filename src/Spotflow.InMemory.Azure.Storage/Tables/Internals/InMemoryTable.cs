using System.Diagnostics.CodeAnalysis;

using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;

using Spotflow.InMemory.Azure.Storage.Internals;

namespace Spotflow.InMemory.Azure.Storage.Tables.Internals;


internal class InMemoryTable(string name, InMemoryTableService service)
{
    private readonly Dictionary<(string PK, string RK), InMemoryTableEntity> _entities = [];
    private readonly string _tableName = name;

    public InMemoryTableService Service { get; } = service;

    private readonly string _accountName = service.Account.Name;
    private readonly TimeProvider _timeProvider = service.Account.Provider.TimeProvider;

    public IReadOnlyList<T> GetEntities<T>(Func<IEnumerable<InMemoryTableEntity>, IEnumerable<T>> filter) where T : ITableEntity
    {
        lock (_entities)
        {
            return filter(_entities.Values).ToList();
        }
    }

    public bool TryUpsertEntity<T>(
        T entity,
        ETag incomingETag,
        TableUpdateMode updateMode,
        bool mustExist,
        [NotNullWhen(true)] out ETag? outgoingETag,
        [NotNullWhen(false)] out EntityUpsertError? error) where T : ITableEntity
    {
        lock (_entities)
        {
            if (!CanUpsertEntityUnsafe(entity.PartitionKey, entity.RowKey, incomingETag, mustExist, out error))
            {
                outgoingETag = null;
                return false;
            }

            UpsertEntityUnsafe(entity, updateMode, out var newEntityETag);

            outgoingETag = newEntityETag;
            error = null;
            return true;
        }
    }

    private void UpsertEntityUnsafe<T>(T entity, TableUpdateMode updateMode, out ETag eTag)
        where T : ITableEntity
    {
        InMemoryTableEntity newEntity;

        var key = (entity.PartitionKey, entity.RowKey);

        if (!_entities.TryGetValue(key, out var existingEntity))
        {
            newEntity = InMemoryTableEntity.CreateNew(entity, _timeProvider);
        }
        else
        {
            newEntity = existingEntity.Update(entity, updateMode, _timeProvider);
        }

        _entities[key] = newEntity;
        eTag = newEntity.ETag;
    }

    private bool CanUpsertEntityUnsafe(string partitionKey, string rowKey, ETag incomingETag, bool mustExist, [NotNullWhen(false)] out EntityUpsertError? error)
    {
        incomingETag.EnsureNotEmpty();

        var key = (partitionKey, rowKey);

        var entityExists = _entities.TryGetValue(key, out var existingEntity);

        if (mustExist)
        {
            if (!entityExists)
            {
                error = new EntityUpsertError.EntityNotFound(_accountName, _tableName, partitionKey, rowKey);
                return false;
            }
        }

        if (!incomingETag.IsEmpty())
        {
            if (!ConditionChecker.CheckConditions(existingEntity?.ETag, ifMatch: incomingETag, null, out var conditionError))
            {
                error = new EntityUpsertError.ConditionNotMet(_accountName, _tableName, partitionKey, rowKey, conditionError);
                return false;
            }
        }

        error = null;
        return true;
    }

    public bool TryAddEntity<T>(T entity, [NotNullWhen(true)] out ETag? eTag, [NotNullWhen(false)] out EntityAddError? error) where T : ITableEntity
    {
        lock (_entities)
        {
            if (!CanAddEntityUnsafe(entity.PartitionKey, entity.RowKey, out error))
            {
                eTag = null;
                return false;
            }

            AddEntityUnsafe(entity, out var eTagUnsafe);
            eTag = eTagUnsafe;
            return true;
        }
    }

    private void AddEntityUnsafe<T>(T entity, out ETag eTag) where T : ITableEntity
    {
        var key = (entity.PartitionKey, entity.RowKey);

        var newEntity = InMemoryTableEntity.CreateNew(entity, _timeProvider);
        _entities[key] = newEntity;
        eTag = newEntity.ETag;
    }

    private bool CanAddEntityUnsafe(string partitionKey, string rowKey, [NotNullWhen(false)] out EntityAddError? error)
    {
        var key = (partitionKey, rowKey);
        if (_entities.ContainsKey(key))
        {
            error = new EntityAddError.EntityAlreadyExists(_accountName, _tableName, partitionKey, rowKey);
            return false;
        }
        else
        {
            error = null;
            return true;
        }
    }



    public bool TryDeleteEntity(string partitionKey, string rowKey, ETag ifMatch, [NotNullWhen(false)] out EntityDeleteError? error)
    {
        if (ifMatch.IsEmpty())
        {
            ifMatch = ETag.All;
        }

        lock (_entities)
        {
            if (!CanDeleteEntityUnsafe(partitionKey, rowKey, ifMatch, out error))
            {
                if (error is EntityDeleteError.NotFound && (ifMatch.IsEmpty() || ifMatch == ETag.All))
                {
                    return true;
                }

                return false;
            }

            DeleteEntityUnsafe(partitionKey, rowKey);
            error = null;
            return true;
        }
    }

    private void DeleteEntityUnsafe(string partitionKey, string rowKey)
    {
        var key = (partitionKey, rowKey);
        _entities.Remove(key);
    }

    private bool CanDeleteEntityUnsafe(string partitionKey, string rowKey, ETag ifMatch, [NotNullWhen(false)] out EntityDeleteError? error)
    {
        var key = (partitionKey, rowKey);

        if (!_entities.TryGetValue(key, out var existingEntity))
        {
            error = new EntityDeleteError.NotFound(_accountName, _tableName, partitionKey, rowKey);
            return false;
        }

        if (!ConditionChecker.CheckConditions(existingEntity?.ETag, ifMatch: ifMatch, null, out var conditionError))
        {
            error = new EntityDeleteError.ConditionNotMet(_accountName, _tableName, partitionKey, rowKey, conditionError);
            return false;
        }

        error = null;
        return true;
    }

    public override string ToString() => $"{Service} / {_tableName}";

    public TableItem AsItem() => TableModelFactory.TableItem(_tableName);

    public bool TrySubmitTransaction(IReadOnlyList<TableTransactionAction> actions, [NotNullWhen(true)] out IReadOnlyList<EntityTransactionResult>? results, [NotNullWhen(false)] out EntityTransactionError? error)
    {
        const int maxEntities = 100;

        if (actions.Count > maxEntities)
        {
            results = null;
            error = new EntityTransactionError.TooManyEntities(_accountName, _tableName, maxEntities, actions.Count);
            return false;
        }

        lock (_entities)
        {
            if (!ValidateTransactionUnsafe(actions, out error))
            {
                results = null;
                return false;
            }

            var entityResults = ExecuteTransactionUnsafe(actions);

            results = entityResults;
            error = null;
            return true;
        }
    }

    private bool ValidateTransactionUnsafe(IReadOnlyList<TableTransactionAction> actions, [NotNullWhen(false)] out EntityTransactionError? error)
    {
        string? partitionKey = null;

        var rowKeys = new HashSet<string>();

        foreach (var action in actions)
        {
            var e = action.Entity;

            var eTag = ResolveETag(action);

            if (!rowKeys.Add(e.RowKey))
            {
                error = new EntityTransactionError.EntityDuplicated(_accountName, _tableName, e.PartitionKey, e.RowKey);
                return false;
            }

            if (partitionKey is null)
            {
                partitionKey = e.PartitionKey;
            }
            else if (partitionKey != e.PartitionKey)
            {
                error = new EntityTransactionError.MultiplePartitionKeys(_accountName, _tableName);
                return false;
            }

            if (action.ActionType is TableTransactionActionType.Add)
            {
                if (!CanAddEntityUnsafe(e.PartitionKey, e.RowKey, out var entityError))
                {
                    error = new EntityTransactionError.FromEntityError(entityError);
                    return false;
                }
            }
            else if (action.ActionType is TableTransactionActionType.UpdateMerge or TableTransactionActionType.UpdateReplace or TableTransactionActionType.UpsertMerge or TableTransactionActionType.UpsertReplace)
            {
                var mustExist = action.ActionType is TableTransactionActionType.UpdateMerge or TableTransactionActionType.UpdateReplace;

                if (!CanUpsertEntityUnsafe(e.PartitionKey, e.RowKey, eTag, mustExist: mustExist, out var entityError))
                {
                    error = new EntityTransactionError.FromEntityError(entityError);

                    return false;
                }
            }
            else if (action.ActionType is TableTransactionActionType.Delete)
            {
                if (!CanDeleteEntityUnsafe(e.PartitionKey, e.RowKey, eTag, out var entityError))
                {
                    error = new EntityTransactionError.FromEntityError(entityError);
                    return false;
                }
            }
            else
            {
                throw new InvalidOperationException($"Unexpected action type: {action.ActionType}");
            }
        }

        error = null;
        return true;
    }

    private static ETag ResolveETag(TableTransactionAction action)
    {
        if (!action.ETag.IsEmpty())
        {
            return action.ETag;
        }

        if (!action.Entity.ETag.IsEmpty())
        {
            return action.Entity.ETag;
        }

        return ETag.All;
    }

    private List<EntityTransactionResult> ExecuteTransactionUnsafe(IReadOnlyList<TableTransactionAction> actions)
    {
        var results = new List<EntityTransactionResult>();

        foreach (var action in actions)
        {
            var e = action.Entity;

            if (action.ActionType is TableTransactionActionType.Add)
            {
                AddEntityUnsafe(e, out var eTag);
                results.Add(new(eTag));
            }
            else if (action.ActionType is TableTransactionActionType.UpdateMerge or TableTransactionActionType.UpsertMerge)
            {
                UpsertEntityUnsafe(e, TableUpdateMode.Merge, out var eTag);
                results.Add(new(eTag));
            }
            else if (action.ActionType is TableTransactionActionType.UpdateReplace or TableTransactionActionType.UpsertReplace)
            {
                UpsertEntityUnsafe(e, TableUpdateMode.Replace, out var eTag);
                results.Add(new(eTag));
            }
            else if (action.ActionType is TableTransactionActionType.Delete)
            {
                DeleteEntityUnsafe(e.PartitionKey, e.RowKey);
                results.Add(new(null));
            }
            else
            {
                throw new InvalidOperationException($"Unexpected action type: {action.ActionType}");
            }

        }

        return results;
    }

    public abstract class EntityError
    {
        public abstract RequestFailedException GetClientException();
    }

    public abstract class EntityAddError() : EntityError
    {
        public class EntityAlreadyExists(string accountName, string tableName, string partitionKey, string rowKey) : EntityAddError
        {
            public override RequestFailedException GetClientException()
            {
                return TableExceptionFactory.EntityAlreadyExists(accountName, tableName, partitionKey, rowKey);
            }
        }
    }

    public abstract class EntityUpsertError() : EntityError()
    {
        public class ConditionNotMet(string accountName, string tableName, string partitionKey, string rowKey, ConditionError error) : EntityUpsertError
        {
            public override RequestFailedException GetClientException()
            {
                return TableExceptionFactory.ConditionNotMet(accountName, tableName, partitionKey, rowKey, error);
            }
        }

        public class EntityNotFound(string accountName, string tableName, string partitionKey, string rowKey) : EntityUpsertError
        {
            public override RequestFailedException GetClientException()
            {
                return TableExceptionFactory.EntityNotFound(accountName, tableName, partitionKey, rowKey);
            }
        }


    }

    public abstract class EntityDeleteError() : EntityError()
    {

        public class NotFound(string accountName, string tableName, string partitionKey, string rowKey) : EntityDeleteError
        {
            public override RequestFailedException GetClientException()
            {
                return TableExceptionFactory.EntityNotFound(accountName, tableName, partitionKey, rowKey);
            }
        }

        public class ConditionNotMet(string accountName, string tableName, string partitionKey, string rowKey, ConditionError error) : EntityDeleteError
        {
            public override RequestFailedException GetClientException()
            {
                return TableExceptionFactory.ConditionNotMet(accountName, tableName, partitionKey, rowKey, error);
            }
        }

    }

    public abstract class EntityTransactionError() : EntityError()
    {
        public class MultiplePartitionKeys(string accountName, string tableName) : EntityTransactionError
        {
            public override RequestFailedException GetClientException()
            {
                return TableExceptionFactory.MultiplePartitionsInTransaction(accountName, tableName);
            }
        }

        public class EntityDuplicated(string accountName, string tableName, string partitionKey, string rowKey) : EntityTransactionError
        {
            public override RequestFailedException GetClientException()
            {
                return TableExceptionFactory.DuplicateEntityInTransaction(accountName, tableName, partitionKey, rowKey);
            }
        }

        public class TooManyEntities(string accountName, string tableName, int maxCount, int actualCount) : EntityTransactionError
        {
            public override RequestFailedException GetClientException()
            {
                return TableExceptionFactory.TooManyEntitiesInTransaction(accountName, tableName, maxCount, actualCount);
            }
        }

        public class FromEntityError(EntityError error) : EntityTransactionError
        {
            public override RequestFailedException GetClientException() => error.GetClientException();
        }
    }

    public record EntityTransactionResult(ETag? ETag);

}
