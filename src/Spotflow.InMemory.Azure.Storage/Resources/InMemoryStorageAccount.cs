using System.Security.Cryptography;
using System.Text;

using Spotflow.InMemory.Azure.Storage.Blobs.Internals;
using Spotflow.InMemory.Azure.Storage.Internals;
using Spotflow.InMemory.Azure.Storage.Tables.Internals;

namespace Spotflow.InMemory.Azure.Storage.Resources;

public class InMemoryStorageAccount
{
    public InMemoryStorageAccount(string name, InMemoryStorageProvider provider)
    {
        Name = name;
        Provider = provider;
        TableService = new(this);
        BlobService = new(this);

        var keySeed = $"{Name}|{Provider.HostnameSuffix}";

        PrimaryAccessKey = Convert.ToBase64String(SHA384.HashData(Encoding.UTF8.GetBytes(keySeed)));

    }

    public string Name { get; }
    public InMemoryStorageProvider Provider { get; }
    public string PrimaryAccessKey { get; }

    internal InMemoryTableService TableService { get; }
    internal InMemoryBlobService BlobService { get; }

    public Uri CreateBlobContainerSasUri(string blobContainerName) => BlobService.CreateContainerSasUri(blobContainerName);

    public Uri CreateBlobSasUri(string blobContainerName, string blobName) => BlobService.CreateBlobSasUri(blobContainerName, blobName);

    public Uri CreateTableSasUri(string tableName) => TableService.CreateTableSasUri(tableName);


    public string CreateConnectionString() => StorageConnectionStringUtils.CreateConnectionString(this);

    public Uri BlobServiceUri => BlobService.Uri;
    public Uri TableServiceUri => TableService.Uri;

    public override string ToString() => Name;

}

