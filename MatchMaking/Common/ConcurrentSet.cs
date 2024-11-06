using System.Collections.Concurrent;

namespace MatchMaking.Common;

public class ConcurrentSet<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, byte> _dictionary = new();

    public bool Add(T item) => _dictionary.TryAdd(item, 0);
    public bool Remove(T item) => _dictionary.TryRemove(item, out _);
    public bool Contains(T item) => _dictionary.ContainsKey(item);
    public IEnumerable<T> Items => _dictionary.Keys;
}
