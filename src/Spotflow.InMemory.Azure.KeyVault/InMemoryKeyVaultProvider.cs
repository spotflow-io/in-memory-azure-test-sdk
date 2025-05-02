using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.Hooks.Internals;
using Spotflow.InMemory.Azure.KeyVault.Hooks;
using Spotflow.InMemory.Azure.KeyVault.Hooks.Contexts;
using Spotflow.InMemory.Azure.KeyVault.Hooks.Internals;
using Spotflow.InMemory.Azure.KeyVault.Resources;

namespace Spotflow.InMemory.Azure.KeyVault;

public class InMemoryKeyVaultProvider(string? hostnameSuffix = null, TimeProvider? timeProvider = null)
{
    private readonly ConcurrentDictionary<Uri, InMemoryKeyVault> _keyVaults = [];

    private readonly HooksExecutor<KeyVaultHookFilter, KeyVaultHookContext> _hooksExecutor = new();
    public TimeProvider TimeProvider { get; } = timeProvider ?? TimeProvider.System;

    public string HostnameSuffix { get; } = hostnameSuffix ?? "keyvault.in-memory.example.com";

    public InMemoryKeyVault AddVault(string? vaultName = null)
    {
        vaultName ??= GenerateVaultName();

        vaultName = vaultName.ToLowerInvariant();

        var vault = new InMemoryKeyVault(vaultName, this);

        if (!_keyVaults.TryAdd(vault.VaultUri, vault))
        {
            throw new InvalidOperationException("Key vault already exists.");
        }

        return vault;
    }

    public bool TryGetVault(string vaultName, [NotNullWhen(true)] out InMemoryKeyVault? result)
    {
        vaultName = vaultName.ToLowerInvariant();

        foreach (var (_, vault) in _keyVaults)
        {
            if (vault.Name == vaultName)
            {
                result = vault;
                return true;
            }
        }

        result = null;
        return false;
    }

    public InMemoryKeyVault GetVault(string vaultName)
    {
        if (!TryGetVault(vaultName, out var vault))
        {
            throw new InvalidOperationException($"Key vault '{vaultName}' not found.");
        }

        return vault;
    }

    public bool TryGetVaultByUri(Uri vaultUri, [NotNullWhen(true)] out InMemoryKeyVault? result)
    {
        return _keyVaults.TryGetValue(vaultUri, out result);
    }

    public InMemoryKeyVault GetVaultByUri(Uri vaultUri)
    {
        if (!TryGetVaultByUri(vaultUri, out var vault))
        {
            throw new InvalidOperationException($"Key vault '{vaultUri}' not found.");
        }

        return vault;
    }

    public IHookRegistration AddHook<TContext>(Func<KeyVaultHookBuilder, KeyVaultHook<TContext>> hook) where TContext : KeyVaultHookContext
    {
        var completedHook = hook(new());

        return _hooksExecutor.AddHook(completedHook.HookFunction, completedHook.Filter);
    }

    internal Task ExecuteHooksAsync<TContext>(TContext context) where TContext : KeyVaultHookContext
    {
        return _hooksExecutor.ExecuteHooksAsync(context);
    }

    private static string GenerateVaultName() => Guid.NewGuid().ToString();

    internal string GetVaultNameFromUri(Uri vaultUri)
    {
        return vaultUri.Host[..(vaultUri.Host.Length - HostnameSuffix.Length - 1)];
    }
}
