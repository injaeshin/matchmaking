
using System.Collections.Concurrent;

namespace MatchMaking.Common
{
    public class Util
    {
        public static long GetUnixTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }

    public class ConcurrentSet<T> where T : notnull
    {
        private readonly ConcurrentDictionary<T, byte> _dictionary = new();

        // 값을 추가하는 메서드 (이미 존재하면 false 반환)
        public bool Add(T item) => _dictionary.TryAdd(item, 0);

        // 값을 제거하는 메서드
        public bool Remove(T item) => _dictionary.TryRemove(item, out _);

        // 값이 존재하는지 확인하는 메서드
        public bool Contains(T item) => _dictionary.ContainsKey(item);

        // 전체 아이템을 열거하는 프로퍼티
        public IEnumerable<T> Items => _dictionary.Keys;
    }
}
