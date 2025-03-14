using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Spotflow.InMemory.Azure.KeyVault.Internals;

internal class SecretsDictionary
{
    private readonly ConcurrentDictionary<string, InMemoryKeyVaultSecret> _inner = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _inner.Count;

    public bool TryGetValue(string secretName, [NotNullWhen(true)] out InMemoryKeyVaultSecret? secret)
    {
        return _inner.TryGetValue(secretName, out secret);
    }

    public bool TryUpdate(string secretName, InMemoryKeyVaultSecret newSecret, InMemoryKeyVaultSecret comparisonSecret)
    {
        return _inner.TryUpdate(secretName, newSecret, comparisonSecret);
    }

    public bool TryAdd(string secretName, InMemoryKeyVaultSecret secret)
    {
        return _inner.TryAdd(secretName, secret);
    }

    public IEnumerator<KeyValuePair<string, InMemoryKeyVaultSecret>> GetEnumerator() => _inner.GetEnumerator();
}
