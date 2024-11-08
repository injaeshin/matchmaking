using MatchMaking.Common;
using MatchMaking.Model;
using StackExchange.Redis;

namespace MatchMaking.Redis
{
    public static class RedisKeys
    {
        //public const string LockValue = "1";
        //public static string MatchLock(int id) => $"match:lock:{id}";

        public static string MatchQueueKey(MatchMode mode) => $"match:queue:{Converter.ToFastString(mode)}";
        public static string MatchScoreKey(MatchMode mode) => $"match:score:{Converter.ToFastString(mode)}";

        public static string MatchRequest => "match:request";
        public static string MatchComplete => "match:complete";
    }

    public class RedisService
    {
        private readonly IDatabase _db = RedisConnection.GetDatabase();
        private readonly RedisMessage _redisMessage = new();
        private readonly Random _random = new();

        public RedisMessage RedisMessage => _redisMessage;

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

        public async Task<long> GetMatchQueueCountAsync(MatchMode mode) =>
            await _db.SortedSetLengthAsync(RedisKeys.MatchQueueKey(mode));

        public async Task<bool> IsEmptyMatchQueueAsync(MatchMode mode) =>
            await _db.SortedSetLengthAsync(RedisKeys.MatchQueueKey(mode)) == 0;

        public async Task<bool> AddMatchQueueAsync(MatchMode mode, MatchQueueItem item) =>
            await _db.SortedSetAddAsync(RedisKeys.MatchQueueKey(mode), item.Id, item.Score);

        public async Task<List<MatchQueueItem>?> GetUserMatchQueueAsync(MatchMode mode, int begin, int end)
        {
            var candidates = await _db.SortedSetRangeByRankWithScoresAsync(RedisKeys.MatchQueueKey(mode), begin, end);
            return candidates?.Select(c => new MatchQueueItem((int)c.Element, 0, (long)c.Score)).ToList();
        }

        public async Task<long> GetUserScoreMatchQueueAsync(MatchMode mode, int id)
        {
            var score = await _db.SortedSetScoreAsync(RedisKeys.MatchQueueKey(mode), id);
            return (score is null) ? 0 : (long)score;
        }

        #endregion

        #region Match Score

        public async Task<List<MatchQueueItem>?> GetMatchCandidatesAsync(MatchMode mode, long minScore, long maxScore, int count)
        {
            long totalCandidates = await _db.SortedSetLengthAsync(RedisKeys.MatchScoreKey(mode), minScore, maxScore);
            if (totalCandidates == 0)
            {
                return null;
            }            

            int offset = _random.Next(0, Math.Max(0, (int)totalCandidates - count));
            var candidates = await _db.SortedSetRangeByScoreWithScoresAsync(
                RedisKeys.MatchScoreKey(mode), minScore, maxScore, Exclude.None, Order.Ascending, offset, count);

            return candidates?.Select(c => new MatchQueueItem((int)c.Element, (int)c.Score/*mmr*/, 0)).ToList();
        }

        public async Task<bool> AddMatchScoreAsync(MatchMode mode, MatchQueueItem item) =>
            await _db.SortedSetAddAsync(RedisKeys.MatchScoreKey(mode), item.Id, item.MMR);

        public async Task<bool> RemoveMatchScoreAsync(MatchMode mode, int id) =>
            await _db.SortedSetRemoveAsync(RedisKeys.MatchScoreKey(mode), id);

        #endregion

        #region Queue Management

        public async Task<bool> RemoveQueueAndScoreAsync(MatchMode mode, int id)
        {
            var transaction = _db.CreateTransaction();
            _ = transaction.SortedSetRemoveAsync(RedisKeys.MatchQueueKey(mode), id);
            _ = transaction.SortedSetRemoveAsync(RedisKeys.MatchScoreKey(mode), id);

            return await transaction.ExecuteAsync();
        }

        public async Task<bool> AddQueueAndScoreAsync(MatchMode mode, MatchQueueItem item)
        {
            var transaction = _db.CreateTransaction();
            _ = transaction.SortedSetAddAsync(RedisKeys.MatchQueueKey(mode), item.Id, item.Score);
            _ = transaction.SortedSetAddAsync(RedisKeys.MatchScoreKey(mode), item.Id, item.MMR);

            return await transaction.ExecuteAsync();
        }

        public async Task<bool> _AddQueueAndScoreAsync(MatchMode mode, IEnumerable<MatchQueueItem> items)
        {
            var transaction = _db.CreateTransaction();
            foreach (var item in items)
            {
                _ = transaction.SortedSetAddAsync(RedisKeys.MatchQueueKey(mode), item.Id, item.Score);
                _ = transaction.SortedSetAddAsync(RedisKeys.MatchScoreKey(mode), item.Id, item.MMR);
            }

            return await transaction.ExecuteAsync();
        }

        public async Task<bool> _RemoveQueueAndScore(MatchMode mode, ICollection<MatchQueueItem> items)
        {
            var transaction = _db.CreateTransaction();
            foreach (var item in items)
            {
                _ = transaction.SortedSetRemoveAsync(RedisKeys.MatchQueueKey(mode), item.Id);
                _ = transaction.SortedSetRemoveAsync(RedisKeys.MatchScoreKey(mode), item.Id);
            }

            return await transaction.ExecuteAsync();
        }

        #endregion
    }
}
