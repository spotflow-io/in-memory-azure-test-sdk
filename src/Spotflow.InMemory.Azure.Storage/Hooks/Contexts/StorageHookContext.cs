using Spotflow.InMemory.Azure.Hooks;

namespace Spotflow.InMemory.Azure.Storage.Hooks.Contexts;

public abstract class StorageHookContext(string storageAccountName, InMemoryStorageProvider provider, CancellationToken cancellationToken)
{
    public string StorageAccountName => storageAccountName;
    public TimeProvider TimeProvider => ResourceProvider.TimeProvider;

    public abstract StorageFaultsBuilder Faults();

    public CancellationToken CancellationToken => cancellationToken;

    public InMemoryStorageProvider ResourceProvider => provider;

    /// <summary>
    /// Adds delay before operations that is generated from <paramref name="delayGenerator"/>. 
    /// </summary>
    public async Task DelayAsync(IDelayGenerator delayGenerator)
    {
        var delay = delayGenerator.Next();
        await Task.Delay(delay, TimeProvider, CancellationToken).ConfigureAwait(ConfigureAwaitOptions.None);
    }

    /// <summary>
    /// Adds constant delay before operations.
    /// </summary>
    public Task DelayAsync(TimeSpan constantDelay)
    {
        return DelayAsync(new ConstantDelayGenerator(constantDelay));
    }

}
