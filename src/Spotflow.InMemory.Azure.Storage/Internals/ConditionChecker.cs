using System.Diagnostics.CodeAnalysis;

using Azure;

using Spotflow.InMemory.Azure.Storage.Tables.Internals;

namespace Spotflow.InMemory.Azure.Storage.Internals;

internal static class ConditionChecker
{
    public static bool CheckConditions(ETag? currentETag, ETag? ifMatch, ETag? ifNoneMatch, [NotNullWhen(false)] out ConditionError? error)
    {
        ifMatch?.EnsureNotEmpty();
        ifNoneMatch?.EnsureNotEmpty();

        if (currentETag is not null && ifNoneMatch == ETag.All)
        {
            error = new ConditionError(ConditionType.IfNoneMatch, "Target already exists.");
            return false;
        }

        if (ifMatch is not null && ifMatch.Value != ETag.All && ifMatch != currentETag)
        {
            error = new ConditionError(ConditionType.IfMatch, "Target has different ETag.");
            return false;
        }

        if (ifNoneMatch is not null && ifNoneMatch == currentETag)
        {
            error = new ConditionError(ConditionType.IfNoneMatch, "Target has the same ETag.");
            return false;
        }

        error = null;
        return true;
    }
}
