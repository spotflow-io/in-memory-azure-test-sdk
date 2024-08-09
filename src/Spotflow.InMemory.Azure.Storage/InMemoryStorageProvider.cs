using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.Hooks.Internals;
using Spotflow.InMemory.Azure.Storage.Hooks;
using Spotflow.InMemory.Azure.Storage.Hooks.Contexts;
using Spotflow.InMemory.Azure.Storage.Hooks.Internals;
using Spotflow.InMemory.Azure.Storage.Resources;

namespace Spotflow.InMemory.Azure.Storage;

public class InMemoryStorageProvider(string? hostnameSuffix = null, TimeProvider? timeProvider = null, ILoggerFactory? loggerFactory = null)
{
    private readonly ConcurrentDictionary<string, InMemoryStorageAccount> _storageAccounts = new();

    private readonly HooksExecutor<StorageHookFilter, StorageHookContext> _hooksExecutor = new();

    internal TimeProvider TimeProvider { get; } = timeProvider ?? TimeProvider.System;
    internal ILoggerFactory LoggerFactory { get; } = loggerFactory ?? NullLoggerFactory.Instance;
    public string HostnameSuffix { get; } = hostnameSuffix ?? "storage.in-memory.example.com";

    public InMemoryStorageAccount AddAccount(string? accountName = null)
    {
        accountName ??= GenerateAccountName();

        accountName = accountName.ToLowerInvariant();

        var storageAccount = new InMemoryStorageAccount(accountName, this);

        if (!_storageAccounts.TryAdd(accountName, storageAccount))
        {
            throw new InvalidOperationException($"Storage account '{accountName}' already exists.");
        }

        return storageAccount;
    }

    public bool TryGetAccount(string accountName, [NotNullWhen(true)] out InMemoryStorageAccount? result)
    {
        accountName = accountName.ToLowerInvariant();

        return _storageAccounts.TryGetValue(accountName, out result);
    }

    public InMemoryStorageAccount GetAccount(string accountName)
    {
        if (!TryGetAccount(accountName, out var account))
        {
            throw new InvalidOperationException($"Storage account '{accountName}' not found.");
        }

        return account;
    }

    public IHookRegistration AddHook<TContext>(Func<StorageHookBuilder, StorageHook<TContext>> hook) where TContext : StorageHookContext
    {
        var completedHook = hook(new());

        return _hooksExecutor.AddHook(completedHook.HookFunction, completedHook.Filter);
    }

    internal Task ExecuteHooksAsync<TContext>(TContext context) where TContext : StorageHookContext
    {
        return _hooksExecutor.ExecuteHooksAsync(context);
    }

    private static string GenerateAccountName() => Guid.NewGuid().ToString().Replace("-", string.Empty)[..24];
}


