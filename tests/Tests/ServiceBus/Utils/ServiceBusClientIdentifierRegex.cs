using System.Text.RegularExpressions;

namespace Tests.ServiceBus;

internal static partial class ServiceBusClientIdentifierRegex
{
    [GeneratedRegex("[A-Z0-9.]+-[A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12}", RegexOptions.IgnoreCase)]
    public static partial Regex IdentifierRegex();
}
