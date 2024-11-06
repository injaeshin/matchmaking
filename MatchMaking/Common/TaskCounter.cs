using System.Collections.Concurrent;

namespace MatchMaking.Common;

public class TaskCounter<T> where T : Enum
{
    private readonly ConcurrentDictionary<T, int> _taskCounts = new();

    public TaskCounter()
    {
        foreach (T tp in Enum.GetValues(typeof(T)))
        {
            _taskCounts.TryAdd(tp, 0);
        }
    }

    public bool Increase(T tp) => _taskCounts.AddOrUpdate(tp, 1, (key, value) => value + 1) == 1;
    public bool Decrease(T tp) => _taskCounts.AddOrUpdate(tp, 0, (key, value) => value - 1) == 1;
    public int Count(T tp) => _taskCounts.TryGetValue(tp, out var count) ? count : 0;
    public int Contains(T tp) => _taskCounts.ContainsKey(tp) ? 1 : 0;
    public void Clear() => _taskCounts.Clear();
}
