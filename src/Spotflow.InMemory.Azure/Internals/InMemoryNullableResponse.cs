using Azure;

namespace Spotflow.InMemory.Azure.Internals;

public class InMemoryNullableResponse<T> : NullableResponse<T>
{
    public override bool HasValue { get; }

    public override T Value { get; }

    private InMemoryNullableResponse(T value, bool hasValue)
    {
        Value = value;
        HasValue = hasValue;
    }

    public static InMemoryNullableResponse<T> FromValue(T value) => new(value, true);

    public static InMemoryNullableResponse<T> FromNull() => new(default!, false);

    public override Response GetRawResponse() => throw new NotSupportedException();
}
