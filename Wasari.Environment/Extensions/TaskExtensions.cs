namespace WasariEnvironment.Extensions;

internal static class TaskExtensions
{
    public static Task<T?> DefaultIfFailed<T>(this Task<T> task)
    {
        return task.ContinueWith(t => t.IsCompletedSuccessfully ? t.Result : default);
    }
}