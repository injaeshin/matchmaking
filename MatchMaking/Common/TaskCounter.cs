using System.Collections.Concurrent;

namespace MatchMaking.Common;

public class TaskWithCancellation(Task task, CancellationTokenSource cts)
{
    public Task Task { get; } = task;
    public CancellationTokenSource CancellationTokenSource { get; } = cts;
}

public class TaskCounter<TEnum> : IDisposable where TEnum : Enum
{
    private const int CooldownPeriod = 2;

    private bool _disposed = false;

    private readonly ConcurrentDictionary<TEnum, long> _lastTaskTime = new();
    private readonly ConcurrentDictionary<TEnum, ConcurrentQueue<TaskWithCancellation>> _tasks = new();

    ~TaskCounter()
    {
        Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        foreach (var queue in _tasks.Values)
        {
            while (queue.Count > 0)
            {
                if (!queue.TryDequeue(out var taskWithCancellation))
                {
                    continue;
                }

                taskWithCancellation.CancellationTokenSource.Cancel();
                taskWithCancellation.CancellationTokenSource.Dispose();
            }
        }

        _disposed = true;
    }

    public async Task IncreaseTask(TEnum taskType, Func<object, CancellationToken, Task> taskAction, object param)
    {
        if (GetTaskCount(taskType) >= Constant.MaxTaskCount)
        {
            Console.WriteLine($"Task Count is Max: {taskType}, {GetTaskCount(taskType)}");
            return;
        }

        if (IsInCooldown(taskType))
        {
            Console.WriteLine($"IncreaseTask is in cooldown period: {taskType}");
            return;
        }

        var cts = new CancellationTokenSource();
        var task = Task.Run(() => taskAction(param, cts.Token), cts.Token);

        var taskWithCancellation = new TaskWithCancellation(task, cts);

        _tasks.AddOrUpdate(
             taskType,
             _ => new ConcurrentQueue<TaskWithCancellation>(new[] { taskWithCancellation }),
             (_, queue) =>
             {
                 queue.Enqueue(taskWithCancellation);
                 return queue;
             });

        UpdateCooldown(taskType);

        Console.WriteLine($"Task increased: {taskType} - qty {GetTaskCount(taskType)}");

        await Task.CompletedTask;
    }

    public async Task DecreaseTask(TEnum taskType)
    {
        if (GetTaskCount(taskType) <= Constant.MinTaskCount)
        {
            Console.WriteLine($"Task Count is Min: {taskType}, {GetTaskCount(taskType)}");
            return;
        }

        if (IsInCooldown(taskType))
        {
            Console.WriteLine($"DecreaseTask is in cooldown period: {taskType}");
            return;
        }

        if (!_tasks.TryGetValue(taskType, out var queue) || queue.IsEmpty)
        {
            return;
        }

        if (!queue.TryDequeue(out var taskWithCancellation))
        {
            return;
        }

        taskWithCancellation.CancellationTokenSource.Cancel();
        taskWithCancellation.CancellationTokenSource.Dispose();
        await taskWithCancellation.Task;

        UpdateCooldown(taskType);

        Console.WriteLine($"Task decreased: {taskType} - qty {GetTaskCount(taskType)}");
    }

    public int GetTaskCount(TEnum taskType)
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

    private bool IsInCooldown(TEnum taskType)
    {
        if (_lastTaskTime.TryGetValue(taskType, out var lastTime))
        {
            return (TimeHelper.GetUnixTimestamp() - lastTime) < CooldownPeriod;
        }
        return false;
    }

    private void UpdateCooldown(TEnum taskType)
    {
        _lastTaskTime[taskType] = TimeHelper.GetUnixTimestamp();
    }
}
