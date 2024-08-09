using System.Diagnostics.CodeAnalysis;
using System.Net;

using Azure;
using Azure.Core;

namespace Spotflow.InMemory.Azure.Internals;

public class InMemoryResponse : Response
{
    private readonly Dictionary<string, List<string>> _headers = [];

    public static Response<T> FromValue<T>(T value, int status, ETag? eTag = null) => FromValue(value, new InMemoryResponse(status, eTag: eTag));


    public InMemoryResponse(int status, ETag? eTag = null)
    {
        Status = status;
        ClientRequestId = Guid.NewGuid().ToString();

        if (eTag != null)
        {
            AddHeaderValue("ETag", eTag.Value.ToString());
        }

    }

    public override int Status { get; }

    public override string ReasonPhrase
    {
        get
        {
            var statusEnum = (HttpStatusCode) Status;

            if (Enum.IsDefined(statusEnum))
            {
                return statusEnum.ToString();
            }

            return Status.ToString();
        }
    }

    public override Stream? ContentStream { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override string ClientRequestId { get; set; }
    public override void Dispose() { }
    protected override bool ContainsHeader(string name) => _headers.ContainsKey(name);

    protected override IEnumerable<HttpHeader> EnumerateHeaders() => _headers.SelectMany(kvp => kvp.Value.Select(v => new HttpHeader(kvp.Key, v)));

    protected override bool TryGetHeader(string name, [NotNullWhen(true)] out string? value)
    {
        if (_headers.TryGetValue(name, out var valuesRaw))
        {
            value = string.Join(",", valuesRaw);
            return true;
        }
        else
        {
            value = null;
            return false;
        }
    }


    protected override bool TryGetHeaderValues(string name, [NotNullWhen(true)] out IEnumerable<string>? values)
    {
        if (_headers.TryGetValue(name, out var valuesRaw))
        {
            values = valuesRaw;
            return true;
        }
        else
        {
            values = null;
            return false;
        }
    }

    private void AddHeaderValue(string headerName, string value)
    {
        if (!_headers.TryGetValue(headerName, out var values))
        {
            values = new(1);
            _headers[headerName] = values;
        }

        values.Add(value);
    }

}
