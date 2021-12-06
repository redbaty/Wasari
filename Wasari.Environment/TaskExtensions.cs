namespace WasariEnvironment;

internal static class TaskExtensions
{
    public static Task<bool> DefaultsToFalse(this Task<bool> task)
    {
        return task.ContinueWith(t => t.IsCompletedSuccessfully && t.Result);
    }
}