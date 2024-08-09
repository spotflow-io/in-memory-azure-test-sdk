<h1 align="center">Azure Key Vault</h1>

<p align="center">This library provides in-memory SDK for Azure Key Vault which can be used as a drop-in replacement for the official 
`Azure.Security.KeyVault.*` SDKs in your tests.</p>

<p align="center">
    <a href="#recommended-usage">Recommended Usage</a> |
    <a href="#features">Features</a> |
    <a href="#available-client-apis">Available APIs</a>
</p>

> [!TIP]
> See the whole [In-Memory Azure Test SDK](../README.md) suite if you are interested in other Azure services.

## Recommended Usage

To get started, add `Spotflow.InMemory.Azure.KeyVault` package to your project.

```shell
dotnet add Spotflow.InMemory.Azure.KeyVault
```

Create non-static factory class for creating the real Azure SDK clients. Relevant methods should be virtual to allow overriding as well as there should be a protected parameterless constructor for testing purposes.

```cs
class AzureClientFactory(TokenCredential tokenCredential)
{
    protected AzureClientFactory(): this(null!) {} // Testing-purposes only

    public virtual SecretClient CreateSecretClient(Uri vaultUri) => new(vaultUri, tokenCredential);
}
```

Use this class to obtain Key Vault clients in the tested code:

```cs
class ExampleService(AzureClientFactory clientFactory, Uri vaultUri)
{
    private readonly SecretClient _client = clientFactory.CreateSecretClient(vaultUri);

    public async Task<string> GetSecretAsync(string secretName)
    {
        var response = await _client.GetSecretAsync(secretName);
        return response.Value.Value;
    }
}
```

Create `InMemoryAzureClientFactory` by inheriting `AzureClientFactory` and override relevant factory methods to return in-memory clients:

```cs
class InMemoryAzureClientFactory(InMemoryEventHubProvider provider): AzureClientFactory
{
    public override SecretClient CreateSecretClient(Uri vaultUri)
    {
        return new InMemorySecretClient(vaultUri, provider);
    }
}
```

When testing, it is now enough to initialize `InMemoryKeyVaultProvider` and inject `InMemoryAzureClientFactory` to the tested code (e.g. via Dependency Injection):

```cs
var provider = new InMemoryKeyVaultProvider();
var vault = provider.AddVault();

var services = new ServiceCollection();

services.AddSingleton<ExampleService>();
services.AddSingleton(provider);
services.AddSingleton<AzureClientFactory, InMemoryAzureClientFactory>();

var exampleService = services.BuildServiceProvider().GetRequiredService<ExampleService>();

var secret = exampleService.GetSecretAsync("my-secret");
```

## Fault Injection

Fault injection is currently not supported for Azure Key Vault.

However, [hooks](hooks.md) can be used to simulate custom faults. For overview of available hooks, please see the [Hooks](#hooks) section.

## Delay Simulation

Delay simulation is currently not supported for Azure Key Vault.

However, [hooks](hooks.md) can be used to simulate custom delays. For overview of available hooks, please see the [Hooks](#hooks) section.

## Supported APIs and features

### SDK clients & methods

Following SDK clients and their method groups and properties are supported.

Async versions of these methods are also supported. All supported async methods are guaranteed executed truly asynchronously by using [Task.Yield()](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.yield) or [ConfigureAwaitOptions.ForceYielding](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.configureawaitoptions).

Other methods and properties are not supported and will throw `NotSupportedException`.

Clients are thread-safe.

#### `InMemorySecretClient : SecretClient`

| Property   | Note |
| ---------- | ---- |
| `VaultUri` |      |

| Method group                    |
| ------------------------------- |
| `GetSecret`                     |
| `SetSecret`                     |
| `GetPropertiesOfSecrets`        |
| `GetPropertiesOfSecretVersions` |
| `StartDeleteSecret`             |
| `UpdateSecretProperties`        |

| Constructors & factory methods      | Note |
| ----------------------------------- | ---- |
| `(Uri vaultUri)`                    |      |
| `FromVault(InMemoryKeyVault vault)` |      |

### Features

For the supported methods enumerated above, not all features are fully implemented.

If the given feature is not supported, than the method will just ignore any parameters related to that feature.

| Feature                                  | Is Supported |
| ---------------------------------------- | ------------ |
| Secrets - Properties - `ContentType`     | ✅           |
| Secrets - Properties - `CreatedOn`       | ✅           |
| Secrets - Properties - `Enabled`         | ✅           |
| Secrets - Properties - `ExpiresOn`       | ✅           |
| Secrets - Properties - `NotBefore`       | ✅           |
| Secrets - Properties - `RecoverableDays` | ❌           |
| Secrets - Properties - `RecoveryLevel`   | ❌           |
| Secrets - Properties - `Tags`            | ✅           |
| Secrets - Properties - `UpdatedOn`       | ✅           |
| Secrets - Purge                          | ❌           |
| Secrets - Recovery                       | ❌           |
| Secrets - Soft-delete                    | ✅           |
| Secrets - Versioning                     | ✅           |

## Hooks

Following hooks are supported in both `Before` and `After` variants:

-   All `Key Vault` operations
    -   All `Secret` operations
        -   `GetSecret`
        -   `SetSecret`

For details about concept of hooks, please see the [Hooks](./hooks.md) page.
