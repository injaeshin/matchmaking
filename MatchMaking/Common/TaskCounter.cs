using System.Collections.Concurrent;

namespace MatchMaking.Common;

public interface ITaskCounter<TEnum> where TEnum : Enum
{
    void IncreaseTask(TEnum taskType, Func<object, CancellationToken, Task> taskAction, object param);
    void DecreaseTask(TEnum taskType);
    void ClearTask(TEnum taskType);
    void Clear();
    int TaskCount(TEnum taskType);
    bool TaskContains(TEnum taskType);
}

public class TaskWithCancellation(Task task, CancellationTokenSource cts)
{
    public Task Task { get; } = task;
    public CancellationTokenSource CancellationTokenSource { get; } = cts;
}

public class TaskCounter<TEnum> : IDisposable where TEnum : Enum
{
    private const int MaxTaskCount = 2;

    private readonly ConcurrentDictionary<TEnum, Queue<TaskWithCancellation>> _tasks = new();

    public void IncreaseTask(TEnum taskType, Func<object, CancellationToken, Task> taskAction, object param)
    {
        if (TaskCount(taskType) >= MaxTaskCount)
        {
            Console.WriteLine($"Task Count is Max: {taskType}, {TaskCount(taskType)}");
            return;
        }

        var cts = new CancellationTokenSource();
        var task = Task.Run(() => taskAction(param, cts.Token), cts.Token);
        var taskWithCancellation = new TaskWithCancellation(task, cts);

        _tasks.AddOrUpdate(
             taskType,
             new Queue<TaskWithCancellation>(new[] { taskWithCancellation }),
             (key, queue) =>
             {
                 lock (queue) { queue.Enqueue(taskWithCancellation); }
                 return queue;
             });
    }

    public async void DecreaseTask(TEnum taskType)
    {
        if (!_tasks.TryGetValue(taskType, out var queue))
        {
            return;
        }

        TaskWithCancellation taskWithCancellation;
        lock (queue)
        {
            if (queue.Count == 0)
            {
                return;
            }

            taskWithCancellation = queue.Dequeue();            
        }

        taskWithCancellation.CancellationTokenSource.Cancel();
        taskWithCancellation.CancellationTokenSource.Dispose();
        await taskWithCancellation.Task;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        foreach (var queue in _tasks.Values)
        {
            lock (queue)
            {
                while (queue.Count > 0)
                {
                    var taskWithCancellation = queue.Dequeue();
                    taskWithCancellation.CancellationTokenSource.Cancel();
                    taskWithCancellation.CancellationTokenSource.Dispose();                    
                }
            }
        }
    }

    public int TaskCount(TEnum taskType)
    {
        if (!_tasks.TryGetValue(taskType, out var queue))
        {
            return 0;
        }

        lock (queue)
        {
            return queue.Count;
        }
    }
}
