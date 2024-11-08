using System.Collections.Concurrent;

namespace MatchMaking.Common;

public class TaskCounter<TEnum> where TEnum : Enum
{
    private const int MaxTaskCount = 2;

    private readonly ConcurrentDictionary<TEnum, Queue<CancellationTokenSource>> _cancelTokens = new();

    // Task를 추가할 때 새로운 CancellationTokenSource를 큐에 넣음
    public void IncreaseTask(TEnum taskType, object param, Func<object, CancellationToken, Task> taskAction)
    {
        if (TaskCount(taskType) >= MaxTaskCount)
        {
            Console.WriteLine($"Task Count is Max: {taskType}, {TaskCount(taskType)}");
            return;
        }

        var cts = new CancellationTokenSource();
        Queue<CancellationTokenSource> updateFunc(TEnum key, Queue<CancellationTokenSource> queue)
        {
            lock (queue) { queue.Enqueue(cts); }
            return queue;
        }

        _cancelTokens.AddOrUpdate(taskType, new Queue<CancellationTokenSource>([cts]), updateFunc);

        var token = cts.Token;
        Task.Run(() => taskAction(param, token), token);
    }

    // Task를 줄일 때 큐에서 하나의 CancellationTokenSource를 꺼내어 취소
    public void DecreaseTask(TEnum taskType)
    {
        if (!_cancelTokens.TryGetValue(taskType, out var queue))
        {
            return;
        }

        lock (queue)
        {
            if (queue.Count > 0)
            {
                var cts = queue.Dequeue();
                cts.Cancel();
                cts.Dispose();
            }
        }
    }

    public void Clear()
    {
        foreach (var queue in _cancelTokens.Values)
        {
            lock (queue)
            {
                while (queue.Count > 0)
                {
                    var cts = queue.Dequeue();
                    cts.Cancel();
                    cts.Dispose();
                }
            }
        }
    }

    public void ClearTask(TEnum taskType)
    {
        if (TaskCount(taskType) == 0)
        {
            return;
        }

        if (!_cancelTokens.TryGetValue(taskType, out var queue))
        {
            return;
        }

        lock (queue)
        {
            while (queue.Count > 0)
            {
                var cts = queue.Dequeue();
                cts.Cancel();
                cts.Dispose();
            }
        }
    }

    public int TaskCount(TEnum taskType)
    {
        if (!_cancelTokens.TryGetValue(taskType, out var queue))
        {
            return 0;
        }

        lock (queue)
        {
            return queue.Count;
        }
    }

    public bool TaskContains(TEnum tp) => _cancelTokens.ContainsKey(tp);

}
