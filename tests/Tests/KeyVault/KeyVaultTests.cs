using Spotflow.InMemory.Azure.KeyVault;

namespace Tests.KeyVault;

[TestClass]
public class KeyVaultTests
{
    [TestMethod]
    public void KeyVault_Should_Be_Case_Insensitive()
    {
        var provider = new InMemoryKeyVaultProvider();

        var vault = provider.AddVault("TestVault");

        provider.GetVault("testvault").Should().BeSameAs(vault);
        provider.GetVault("TESTVAULT").Should().BeSameAs(vault);

        provider
            .GetVaultByUri(new Uri("https://testvault.keyvault.in-memory.example.com"))
            .Should()
            .BeSameAs(vault);

    }
}
