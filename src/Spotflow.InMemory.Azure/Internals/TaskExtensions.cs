namespace Spotflow.InMemory.Azure.Internals;

internal static class TaskExtensions
{
    public static T EnsureCompleted<T>(this Task<T> task)
    {
        return task.GetAwaiter().GetResult();
    }

}
