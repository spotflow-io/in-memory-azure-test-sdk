using System.Diagnostics.CodeAnalysis;

using Azure;

using Spotflow.InMemory.Azure.Storage.Resources;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Internals;

internal class InMemoryBlobService(InMemoryStorageAccount account)
{
    private readonly object _syncObj = new();

    private readonly Dictionary<string, InMemoryBlobContainer> _containers = [];

    public Uri Uri { get; } = CreateServiceUriFromAccountName(account.Name, account.Provider);

    public static int MaxBlockCount { get; } = 50_000;
    public static int MaxUncommitedBlocks { get; } = 100_000;
    public static int MaxBlockSize { get; } = 2000 * 1024 * 1024;

    public InMemoryStorageAccount Account { get; } = account;

    public bool TryAddBlobContainer(
        string blobContainerName,
        IDictionary<string, string>? metadata,
        [NotNullWhen(true)] out InMemoryBlobContainer? result,
        [NotNullWhen(false)] out CreateContainerError? error)
    {
        if (!ValidateContainerName(blobContainerName))
        {
            result = null;
            error = new CreateContainerError.InvalidContainerName(Account.Name, blobContainerName);
            return false;
        }


        lock (_syncObj)
        {
            if (_containers.TryGetValue(blobContainerName, out var existingContainer))
            {
                result = null;
                error = new CreateContainerError.ContainerAlreadyExists(existingContainer);
                return false;
            }

            var newContainer = new InMemoryBlobContainer(blobContainerName, metadata, this);

            _containers.Add(blobContainerName, newContainer);

            result = newContainer;
            error = null;
            return true;
        }

    }

    public bool TryGetBlobContainer(string blobContainerName, [NotNullWhen(true)] out InMemoryBlobContainer? container)
    {
        lock (_syncObj)
        {
            return _containers.TryGetValue(blobContainerName, out container);
        }
    }

    public override string ToString() => Uri.ToString().TrimEnd('/');

    public bool ContainerExists(string name) => TryGetBlobContainer(name, out _);

    public static Uri CreateServiceUriFromAccountName(string accountName, InMemoryStorageProvider provider)
    {
        return new($"https://{accountName}.blob.{provider.HostnameSuffix}");
    }

    private static bool ValidateContainerName(string blobContainerName)
    {
        if (blobContainerName.Length < 3 || blobContainerName.Length > 63)
        {
            return false;
        }

        if (!char.IsLetterOrDigit(blobContainerName[0]))
        {
            return false;
        }

        if (!char.IsLetterOrDigit(blobContainerName[^1]))
        {
            return false;
        }

        if (blobContainerName.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
        {
            return false;
        }

        if (blobContainerName.Contains("--"))
        {
            return false;
        }

        return true;
    }

    public Uri GetBlobSasUri(string blobContainerName, string blobName) => BlobUriUtils.UriForBlob(Uri, blobContainerName, blobName);

    public Uri GetContainerSasUri(string blobContainerName) => BlobUriUtils.UriForContainer(Uri, blobContainerName);

    public abstract class CreateContainerError
    {
        public abstract RequestFailedException GetClientException();

        public class ContainerAlreadyExists(InMemoryBlobContainer existingContainer) : CreateContainerError
        {
            public InMemoryBlobContainer ExistingContainer => existingContainer;

            public override RequestFailedException GetClientException()
            {
                return BlobExceptionFactory.ContainerAlreadyExists(existingContainer.AccountName, existingContainer.Name);
            }
        }

        public class InvalidContainerName(string accountName, string blobContainerName) : CreateContainerError
        {
            public override RequestFailedException GetClientException()
            {
                return BlobExceptionFactory.InvalidContainerName(accountName, blobContainerName);
            }
        }
    }
}
