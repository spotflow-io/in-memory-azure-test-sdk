<h1 align="center">Azure Key Vault</h1>

<p align="center">This library provides in-memory SDK for Azure Key Vault which can be used as a drop-in replacement for the official 
`Azure.Security.KeyVault.*` SDKs in your tests.</p>

<p align="center">
    <a href="#example-usage">Example Usage</a> |
    <a href="#features">Features</a> |
    <a href="#available-client-apis">Available APIs</a>
</p>

> [!TIP]
> See the whole [In-Memory Azure Test SDK](../README.md) suite if you are interested in other Azure services.

## Example Usage

To get started, add `Spotflow.InMemory.Azure.KeyVault` package to your project.

```shell
dotnet add Spotflow.InMemory.Azure.KeyVault
```
This package provides in-memory implementation of Azure Key Vault SDK clients and models.
These in-memory implementations are inheriting the real Azure SDK types so you can use them as a drop-in replacement in your tests.
There is nothing special about the in-memory types, so they can be injected in many ways, e.g. via DI and constructor injection as demonstrated below.
Only extra step is to create parent `InMemoryKeyVaultProvider` instance for the in-memory clients.

Let's consider the following type `ExampleService` as an example:

```cs
class ExampleService(SecretClient client)
{
    public async Task<string> GetSecretAsync(string secretName)
    {
        var response = await client.GetSecretAsync(secretName);
        return response.Value.Value;
    }
}
```

The `ExampleService` might be constructed, for example, using DI:

```cs
// Setup DI - production configuration
var vaultUri = "https://test-vault...";
var credential = new DefaultAzureCredential();

var services = new ServiceCollection();

services.AddSingleton<SecretClient>(new SecretClient(vaultUri, credential));
services.AddSingleton<ExampleService>();
...

// Use resulting service provider
var service = services.BuildServiceProvider().GetRequiredService<ExampleService>();
```

*Note:
Most frequently, the `new ServiceCollection()` and `.BuildServiceProvider()` will called by ASP.NET or other frameworks.
This is just an example of one of many ways how the in-memory clients can be used.*


To inject the in-memory implementation of `SecretClient` to the `ExampleService` during test,
the `InMemorySecretClient` can be simply substituted for the real `SecretClient` in the DI container:

```csharp
// Setup DI - test-only configuration (additive)
var inMemoryProvider = new InMemoryKeyVaultProvider();
var secretClient = new InMemorySecretClient(vaultUri, inMemoryProvider);

services.AddSingleton<SecretClient>(secretClient);
```

By default, the `InMemoryKeyVaultProvider` is empty but exposes methods that allow to set up expected management-plane state:

```csharp
inMemoryProvider.AddVault();
```

To set up expected data-plane state, the `InMemorySecretClient` or other in-memory clients can be directly used:

```cs
secretClient.SetSecret("secret-name", secretValue);
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

Async versions of these methods are also supported. All supported async methods are guaranteed executed truly asynchronously by using [Task.Yield()](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.yield).

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
