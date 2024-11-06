using MatchMaking.Model;
using StackExchange.Redis;

namespace MatchMaking.Data
{
    public static class RedisKeys
    {
        public const string LockValue = "1";
        public const string MatchQueueKey = "match:queue";
        public const string MatchScoreKey = "match:score";

        public static string MatchLock(int id) => $"match:lock:{id}";
    }

    public class RedisService
    {
        private readonly IDatabase _db;
        private readonly RedisLock _redisLock;

        public RedisService()
        {
            _db = RedisConnection.GetDatabase();
            _redisLock = new RedisLock();
        }

        #region Lock
        //public async Task<bool> AddLock(int id)
        //{
        //    string script = @"
        //        if redis.call('exists', KEYS[1]) == 0 then
        //            return redis.call('set', KEYS[1], ARGV[1], 'PX', ARGV[2])
        //        else
        //            return 0
        //        end";

        //    var isLockAcquired = (await _db.ScriptEvaluateAsync(script,
        //                                    new RedisKey[] { RedisKeys.MatchLock(id) },
        //                                    new RedisValue[] { RedisKeys.LockValue, _redisLock.LockDuration.TotalMilliseconds }))?.ToString() == "OK";
        //    return isLockAcquired;
        //}
        //public async Task<bool> RemoveLock(int id)
        //{
        //    string script = @"
        //        if redis.call('exists', KEYS[1]) == 1 then
        //            return redis.call('del', KEYS[1])
        //        else
        //            return 0
        //        end";

        //    return (int)await _db.ScriptEvaluateAsync(script, new RedisKey[] { RedisKeys.MatchLock(id) }) == 1;
        //}
        //public async Task<bool> IsLock(int id)
        //{
        //    var ret = await _db.StringGetAsync(RedisKeys.MatchLock(id));
        //    return (ret.IsNullOrEmpty) ? false : true;
        //}
        #endregion

        #region Match Queue

        public async Task<long> GetMatchQueueCount() =>
            await _db.SortedSetLengthAsync(RedisKeys.MatchQueueKey);

        public async Task<bool> IsEmptyMatchQueueAsync() =>
            await _db.SortedSetLengthAsync(RedisKeys.MatchQueueKey) == 0;

        public async Task<bool> AddMatchQueue(MatchQueueItem item) =>
            await _db.SortedSetAddAsync(RedisKeys.MatchQueueKey, item.Id, item.Score);

        public async Task<List<MatchQueueItem>?> GetUserMatchQueue(int begin, int end)
        {
            var candidates = await _db.SortedSetRangeByRankWithScoresAsync(RedisKeys.MatchQueueKey, begin, end);
            return candidates?.Select(c => new MatchQueueItem((int)c.Element, 0, (long)c.Score)).ToList();
        }

        public async Task<long> GetUserScoreMatchQueue(int id)
        {
            var score = await _db.SortedSetScoreAsync(RedisKeys.MatchQueueKey, id);
            return (score == null) ? 0 : (long)score;
        }

        #endregion

        #region Match Score

        public async Task<List<MatchQueueItem>?> GetMatchCandidates(long minScore, long maxScore, int count)
        {
            long totalCandidates = await _db.SortedSetLengthAsync(RedisKeys.MatchScoreKey, minScore, maxScore);
            if (totalCandidates == 0) return null;

            int offset = new Random().Next(0, Math.Max(0, (int)totalCandidates - count));
            var candidates = await _db.SortedSetRangeByScoreWithScoresAsync(
                RedisKeys.MatchScoreKey, minScore, maxScore, Exclude.None, Order.Ascending, offset, count);

            return candidates?.Select(c => new MatchQueueItem((int)c.Element, (int)c.Score, 0)).ToList();
        }

        public async Task<bool> AddMatchScore(MatchQueueItem item) =>
            await _db.SortedSetAddAsync(RedisKeys.MatchScoreKey, item.Id, item.MMR);

        public async Task<bool> RemoveMatchScore(int id) =>
            await _db.SortedSetRemoveAsync(RedisKeys.MatchScoreKey, id);

        #endregion

        #region Queue Management

        public async Task<bool> RemoveQueueAndScore(int id)
        {
            var transaction = _db.CreateTransaction();
            _ = transaction.SortedSetRemoveAsync(RedisKeys.MatchQueueKey, id);
            _ = transaction.SortedSetRemoveAsync(RedisKeys.MatchScoreKey, id);

            return await transaction.ExecuteAsync();
        }

        public async Task<bool> AddQueueAndScore(MatchQueueItem item)
        {
            var transaction = _db.CreateTransaction();
            _ = transaction.SortedSetAddAsync(RedisKeys.MatchQueueKey, item.Id, item.Score);
            _ = transaction.SortedSetAddAsync(RedisKeys.MatchScoreKey, item.Id, item.MMR);

            return await transaction.ExecuteAsync();
        }

        public async Task<bool> _AddQueueAndScore(IEnumerable<MatchQueueItem> items)
        {
            var transaction = _db.CreateTransaction();
            foreach (var item in items)
            {
                _ = transaction.SortedSetAddAsync(RedisKeys.MatchQueueKey, item.Id, item.Score);
                _ = transaction.SortedSetAddAsync(RedisKeys.MatchScoreKey, item.Id, item.MMR);
            }

            return await transaction.ExecuteAsync();
        }

        public async Task<bool> _RemoveQueueAndScore(IEnumerable<MatchQueueItem> items)
        {
            var transaction = _db.CreateTransaction();
            foreach (var item in items)
            {
                _ = transaction.SortedSetRemoveAsync(RedisKeys.MatchQueueKey, item.Id);
                _ = transaction.SortedSetRemoveAsync(RedisKeys.MatchScoreKey, item.Id);
            }

            return await transaction.ExecuteAsync();
        }

        #endregion
    }
}
