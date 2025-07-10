using System.Reflection;

namespace Spotflow.InMemory.Azure.Internals;

internal static class ReflectionUtils
{
    public static TOut? ReadInternalReferenceProperty<TOut>(object obj, string propertyName) where TOut : class
    {
        if (!TryReadOptionalInternalReferenceProperty(obj, propertyName, out TOut? value))
        {
            throw new InvalidOperationException($"Property '{propertyName}' not found on type {obj.GetType().Name}");
        }

        return value;
    }

    public static bool TryReadOptionalInternalReferenceProperty<TOut>(object obj, string propertyName, out TOut? value) where TOut : class
    {
        value = null;

        if (!TryReadOptionalInternalProperty(obj, propertyName, out var rawValue))
        {
            return false;
        }

        if (rawValue is null)
        {
            return true; // null is a valid value for reference types
        }

        if (!rawValue.GetType().IsAssignableTo(typeof(TOut)))
        {
            throw new InvalidOperationException($"Property '{propertyName}' is not assignable to {typeof(TOut).Name}");
        }

        value = (TOut?) rawValue;
        return true;
    }

    public static TOut ReadInternalValueProperty<TOut>(object obj, string propertyName) where TOut : struct
    {
        if (!TryReadOptionalInternalProperty(obj, propertyName, out var value))
        {
            throw new InvalidOperationException($"Property '{propertyName}' not found on type {obj.GetType().Name}");
        }

        if (value is not TOut)
        {
            throw new InvalidOperationException($"Property '{propertyName}' is not of type {typeof(TOut).Name}");
        }

        return (TOut) value;
    }

    private static bool TryReadOptionalInternalProperty(object obj, string propertyName, out object? value)
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        var property = obj.GetType().GetProperty(propertyName, bindingFlags);

        if (property is null)
        {
            value = null;
            return false;
        }

        value = property.GetValue(obj);
        return true;
    }

}
