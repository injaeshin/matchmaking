using RedLockNet;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;

namespace MatchMaking.Redis
{
    public class RedisLock
    {
        private readonly RedLockFactory _redLockFactory;

        private readonly TimeSpan _lockDuration = TimeSpan.FromSeconds(7);
        public TimeSpan LockDuration => _lockDuration;

        public RedisLock()
        {
            var redisConnection = RedisConnection.Connection;
            var redLockEndpoints = new List<RedLockEndPoint>();
            foreach (var endPoint in redisConnection.GetEndPoints())
            {
                redLockEndpoints.Add(new RedLockEndPoint(endPoint));
            }

            _redLockFactory = RedLockFactory.Create(redLockEndpoints);
        }

        public async Task<IRedLock> CreateLockAsync(string resource, TimeSpan expiryTime)
        {
            return await _redLockFactory.CreateLockAsync(resource, expiryTime);
        }

        public async Task ReleaseLockAsync(IRedLock redLock)
        {
            if (redLock.IsAcquired)
            {
                await redLock.DisposeAsync();
            }
        }

        public async Task<T> TryLockAndRunAsync<T>(string resource, TimeSpan expiryTime, Func<Task<T>> func)
        {
            using IRedLock redLock = await _redLockFactory.CreateLockAsync(resource, expiryTime);
            if (!redLock.IsAcquired)
            {
                return default!;
            }

            return await func();
        }

        public async Task<T> TryLockAndRunAsync<T>(string resource, TimeSpan expiryTime, Func<object, Task<T>> func, object param)
        {
            using IRedLock redLock = await _redLockFactory.CreateLockAsync(resource, expiryTime);
            if (!redLock.IsAcquired)
            {
                return default!;
            }

            return await func(param);
        }
    }
}
