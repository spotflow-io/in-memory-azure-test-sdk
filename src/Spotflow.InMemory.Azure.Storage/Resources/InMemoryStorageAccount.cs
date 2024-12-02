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

    public Uri GetBlobContainerSasUri(string blobContainerName) => BlobService.GetContainerSasUri(blobContainerName);

    public Uri GetBlobSasUri(string blobContainerName, string blobName) => BlobService.GetBlobSasUri(blobContainerName, blobName);

    public Uri GetTableSasUri(string tableName) => TableService.GetTableSasUri(tableName);

    public string GetConnectionString() => StorageConnectionStringUtils.GetConnectionString(this);

    public Uri BlobServiceUri => BlobService.Uri;
    public Uri TableServiceUri => TableService.Uri;

    public override string ToString() => Name;

}

