namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks.Contexts;

public class TableCreateBeforeHookContext(TableScope scope, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : TableBeforeHookContext(scope, TableOperations.Create, provider, cancellationToken);
