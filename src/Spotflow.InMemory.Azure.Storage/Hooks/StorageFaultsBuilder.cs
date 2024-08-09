namespace Spotflow.InMemory.Azure.Storage.Hooks;

public abstract class StorageFaultsBuilder
{
    public abstract Task ServiceIsBusy();
}

