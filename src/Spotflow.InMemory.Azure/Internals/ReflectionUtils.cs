using System.Reflection;

namespace Spotflow.InMemory.Azure.Internals;

internal static class ReflectionUtils
{

    public static TOut? ReadInternalReferenceProperty<TOut>(object obj, string propertyName) where TOut : class
    {
        var value = ReadInternalProperty(obj, propertyName);

        if (value is null)
        {
            return null;
        }

        if (!value.GetType().IsAssignableTo(typeof(TOut)))
        {
            throw new InvalidOperationException($"Property '{propertyName}' is not assignable to {typeof(TOut).Name}");
        }

        return (TOut?) value;

    }

    public static TOut ReadInternalValueProperty<TOut>(object obj, string propertyName) where TOut : struct
    {
        var value = ReadInternalProperty(obj, propertyName);

        if (value is not TOut)
        {
            throw new InvalidOperationException($"Property '{propertyName}' is not of type {typeof(TOut).Name}");
        }

        return (TOut) value;
    }

    private static object? ReadInternalProperty(object obj, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(obj);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        var property = obj.GetType().GetProperty(propertyName, bindingFlags);

        if (property is null)
        {
            throw new InvalidOperationException($"Property '{propertyName}' not found on type {obj.GetType().Name}");
        }

        return property.GetValue(obj);
    }

}
