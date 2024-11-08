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
            var redLockEndpoints = new List<RedLockEndPoint>
            {
                new(redisConnection.GetEndPoints().First())
            };

            _redLockFactory = RedLockFactory.Create(redLockEndpoints);
        }

        public async Task<IRedLock> CreateLockAsync(string resource, TimeSpan expiryTime)
        {
            return await _redLockFactory.CreateLockAsync(resource, expiryTime);
        }

        public async Task<bool> TryAcquireLockAsync(string resource, TimeSpan expiryTime)
        {
            var redLock = await _redLockFactory.CreateLockAsync(resource, expiryTime);
            if (redLock.IsAcquired)
            {
                return true;
            }

            return false;
        }

        public async Task ReleaseLockAsync(IRedLock redLock)
        {
            if (redLock.IsAcquired)
            {
                await redLock.DisposeAsync();
            }
        }
    }
}
