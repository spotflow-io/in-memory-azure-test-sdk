using Spotflow.InMemory.Azure.Storage.Internals;

namespace Tests.Storage;

[TestClass]
public class StorageConnectionStringUtilsTests
{
    [TestMethod]
    [DataRow("AccountName=testaccount;AccountKey=gnmZeqVpQvbeId1SPiFaM8N80Oj/Z87v4EX83WpVwFUkcSlz/Cs1VNaLkZl/8wlD8GVUvgQ4AjgZ+AStGOFDmQ==", DisplayName = "WithoutTrailingSemicolon")]
    [DataRow("AccountName=testaccount;AccountKey=gnmZeqVpQvbeId1SPiFaM8N80Oj/Z87v4EX83WpVwFUkcSlz/Cs1VNaLkZl/8wlD8GVUvgQ4AjgZ+AStGOFDmQ==;", DisplayName = "WithTrailingSemicolon")]
    [DataRow("BlobEndpoint=https://testaccount.blob.core.windows.net;AccountKey=gnmZeqVpQvbeId1SPiFaM8N80Oj/Z87v4EX83WpVwFUkcSlz/Cs1VNaLkZl/8wlD8GVUvgQ4AjgZ+AStGOFDmQ==;", DisplayName = "WithEndpoint")]
    public void Connection_String_Should_Be_Parsed_Properly(string connectionString)
    {
        var result = StorageConnectionStringUtils.TryGetSharedKey(connectionString, out var credential);

        result.Should().BeTrue();
        credential.Should().NotBeNull();
        credential!.AccountName.Should().Be("testaccount");
    }
}
