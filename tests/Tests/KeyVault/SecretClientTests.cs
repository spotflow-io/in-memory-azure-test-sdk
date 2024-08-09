using Azure;
using Azure.Security.KeyVault.Secrets;

using Spotflow.InMemory.Azure.KeyVault;
using Spotflow.InMemory.Azure.KeyVault.Secrets;
using Spotflow.InMemory.Azure.KeyVault.Secrets.Hooks.Contexts;

using Tests.Utils;

namespace Tests.KeyVault;

[TestClass]
public class SecretClientTests
{
    [TestMethod]
    public void Constructor_ShouldSucceed()
    {
        var vault = new InMemoryKeyVaultProvider().AddVault();

        var client = new InMemorySecretClient(vault.VaultUri, vault.Provider);

        client.VaultUri.Should().Be(vault.VaultUri);
    }


    [TestMethod]
    public void Constructor_FromVault_ShouldSucceed()
    {
        var vault = new InMemoryKeyVaultProvider().AddVault();

        var client = InMemorySecretClient.FromVault(vault);

        client.VaultUri.Should().Be(vault.VaultUri);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void SetSecret_New_Should_Succeed()
    {
        var client = ImplementationProvider.GetSecretClient();

        var secretName = Guid.NewGuid().ToString();

        try
        {
            var response = client.SetSecret(secretName, "test-value");

            var version = response.Value.Properties.Version;

            var expectedSecretId = new Uri(client.VaultUri, $"/secrets/{secretName}/{version}");

            response.Value.Name.Should().Be(secretName);
            response.Value.Value.Should().Be("test-value");
            response.Value.Properties.Version.Should().HaveLength(32);
            response.Value.Id.Should().Be(expectedSecretId);
            response.Value.Properties.CreatedOn.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
            response.Value.Properties.UpdatedOn.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
            response.Value.Properties.ContentType.Should().BeNull();
            response.Value.Properties.Tags.Should().BeEmpty();
        }
        finally
        {
            Task.Run(() => client.StartDeleteSecret(secretName));
        }
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void GetSecret_Missing_Should_Fail()
    {
        var client = ImplementationProvider.GetSecretClient();

        var secretName = Guid.NewGuid().ToString();

        var act = () => client.GetSecret(secretName);

        act.Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 404)
            .Where(e => e.ErrorCode == "SecretNotFound");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void GetSecret_Existing_Should_Return_Latest_Version()
    {
        var client = ImplementationProvider.GetSecretClient();

        var secretName = Guid.NewGuid().ToString();

        try
        {
            client.SetSecret(secretName, "test-value-1");

            client.GetSecret(secretName).Value.Value.Should().Be("test-value-1");

            client.SetSecret(secretName, "test-value-2");

            client.GetSecret(secretName).Value.Value.Should().Be("test-value-2");
        }
        finally
        {
            Task.Run(() => client.StartDeleteSecret(secretName));
        }
    }


    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void GetPropertiesOfSecrets_Should_List_Active_Secrets()
    {
        var client = ImplementationProvider.GetSecretClient();

        var secretName = Guid.NewGuid().ToString();

        try
        {
            client.SetSecret(secretName, "test-value");

            client.GetPropertiesOfSecrets().Should().ContainSingle(s => s.Name == secretName);

            client.StartDeleteSecret(secretName).WaitForCompletion();

            client.GetPropertiesOfSecrets().Should().BeEmpty();

        }
        finally
        {
            Task.Run(() => client.StartDeleteSecret(secretName));
        }
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void StartDeleteSecret_Should_Make_Secret_Not_Listed()
    {
        var client = ImplementationProvider.GetSecretClient();

        var secretName = Guid.NewGuid().ToString();

        client.SetSecret(secretName, "test-value");

        var secretsBeforeDelete = client.GetPropertiesOfSecrets().Where(s => s.Name == secretName).ToList();

        secretsBeforeDelete.Should().ContainSingle();

        client.StartDeleteSecret(secretName);

        var secretsAfterDelete = client.GetPropertiesOfSecrets().Where(s => s.Name == secretName).ToList();

        secretsAfterDelete.Should().BeEmpty();
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void Get_Disabled_Secret_Should_Fail_But_Set_Should_Succeed()
    {
        var client = ImplementationProvider.GetSecretClient();

        var secretName = Guid.NewGuid().ToString();

        var secret = new KeyVaultSecret(secretName, "test-value");
        secret.Properties.Enabled = false;

        client.SetSecret(secret);

        try
        {
            var getAct = () => client.GetSecret(secretName);
            var setAct = () => client.SetSecret(secretName, "new-value");

            getAct.Should()
                .Throw<RequestFailedException>()
                .Where(ex => ex.Status == 403)
                .Where(ex => ex.ErrorCode == "Forbidden")
                .WithMessage("Operation get is not allowed on a disabled secret.*");

            setAct.Should().NotThrow();

        }
        finally
        {
            client.StartDeleteSecret(secretName);
        }

    }

    [TestMethod]
    public void SetSecret_And_GetSecret_Hooks_Should_Be_Called()
    {
        var provider = new InMemoryKeyVaultProvider();

        SetSecretBeforeHookContext? capturedBeforeSet = null;
        SetSecretAfterHookContext? capturedAfterSet = null;
        GetSecretBeforeHookContext? capturedBeforeGet = null;
        GetSecretAfterHookContext? capturedAfterGet = null;

        provider.AddHook(hook => hook.ForSecrets().BeforeSetSecret(ctx =>
        {
            capturedBeforeSet = ctx;
            return Task.CompletedTask;
        }));

        provider.AddHook(hook => hook.ForSecrets().AfterSetSecret(ctx =>
        {
            capturedAfterSet = ctx;
            return Task.CompletedTask;
        }));

        provider.AddHook(hook => hook.ForSecrets().BeforeGetSecret(ctx =>
        {
            capturedBeforeGet = ctx;
            return Task.CompletedTask;
        }));

        provider.AddHook(hook => hook.ForSecrets().AfterGetSecret(ctx =>
        {
            capturedAfterGet = ctx;
            return Task.CompletedTask;
        }));

        var vault = provider.AddVault();

        var client = InMemorySecretClient.FromVault(vault);

        client.SetSecret("name", "value");
        client.GetSecret("name");

        capturedBeforeSet.Should().NotBeNull();
        capturedAfterSet.Should().NotBeNull();
        capturedBeforeGet.Should().NotBeNull();
        capturedAfterGet.Should().NotBeNull();

        capturedBeforeSet!.Secret.Value.Should().Be("value");
        capturedAfterSet!.CreatedSecret.Properties.Version.Should().MatchRegex("[a-f0-9]{32}");
        capturedAfterGet!.Secret.Value.Should().Be("value");

    }

}
