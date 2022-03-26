using System.Threading.Channels;

namespace Wasari.App;

public class TaskPool<T> where T : Task
{
    public TaskPool(int size)
    {
        Size = size;
        CurrentRunningTasks = new List<Task>(Size);
        CurrentQueueTask = QueueTask();
    }

    private int Size { get; }

    public int EnqueuedCount { get; set; }

    private List<Task> CurrentRunningTasks { get; }

    private Task? CurrentQueueTask { get; set; }

    private Channel<Func<T>> TaskToCreate { get; } = Channel.CreateUnbounded<Func<T>>();

    private Channel<T> TasksCompleted { get; } = Channel.CreateUnbounded<T>();

    public ChannelReader<T> TasksCompletedReader => TasksCompleted.Reader;

    private async Task QueueTask()
    {
        await foreach (var factory in TaskToCreate.Reader.ReadAllAsync())
        {
            if (CurrentRunningTasks.Count >= Size)
            {
                var ran = await Task.WhenAny(CurrentRunningTasks);
                CurrentRunningTasks.Remove(ran);
            }

            var task = factory().ContinueWith(async t =>
            {
                if (!t.IsCompletedSuccessfully)
                {
                    var exception = t.Exception ?? new Exception("Failed while running task");
                    TasksCompleted.Writer.Complete(exception);
                    throw exception;
                }

                await TasksCompleted.Writer.WriteAsync((T)t);
            }).Unwrap();

            CurrentRunningTasks.Add(task);
        }
    }

    public async Task Add(Func<T> task)
    {
        await TaskToCreate.Writer.WriteAsync(task);
        EnqueuedCount++;
    }

    public async Task WaitToReachEnqueuedCount()
    {
        var tasksCompleted = 0;

        await foreach (var _ in TasksCompletedReader.ReadAllAsync())
        {
            tasksCompleted++;
            if (tasksCompleted == EnqueuedCount)
                break;
        }
    }

    public async Task WaitAndClose()
    {
        TaskToCreate.Writer.Complete();

        if (CurrentQueueTask != null)
        {
            await CurrentQueueTask;
            CurrentQueueTask = null;
        }

        await Task.WhenAll(CurrentRunningTasks);
    }
}