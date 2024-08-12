namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks.Contexts;

public class TableQueryBeforeHookContext(TableScope scope, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : TableBeforeHookContext(scope, TableOperations.Query, provider, cancellationToken);
