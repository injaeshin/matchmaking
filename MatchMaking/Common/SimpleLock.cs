namespace MatchMaking.Common
{
    public class SimpleLock<T> where T : notnull
    {
        private readonly ConcurrentSet<T> _locks = new();

        public bool TryLock(T key) => _locks.Add(key);
        
        public bool Unlock(T key) => _locks.Remove(key);

        public bool IsLocked(T key) => _locks.Contains(key);
    }
}
