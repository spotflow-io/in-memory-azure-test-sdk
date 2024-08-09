using Spotflow.InMemory.Azure.Storage;

namespace Tests.Storage;

[TestClass]
public class StorageProviderTests

{
    [TestMethod]
    public void Get_Non_Existent_Account_Should_Throw()
    {
        var provider = new InMemoryStorageProvider();

        Action act = () => provider.GetAccount("non-existent");

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Storage account 'non-existent' not found.");
    }
}
