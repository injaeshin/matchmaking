using MatchMaking.Common;
using MatchMaking.Model;
using StackExchange.Redis;

namespace MatchMaking.Redis
{
    public class RedisService
    {
        private readonly Random _random = new();
        private readonly IConnectionMultiplexer _connection;
        private readonly IDatabase _db;

        public RedisMessage RedisMessage { get; } = new();

        public RedisService(IConnectionMultiplexer connection)
        {
            _connection = connection;
            _db = _connection.GetDatabase();

            Init();
        }

        private void Init()
        {
            ClearAllMatchUsersAsync();

            foreach (MatchMode mode in Enum.GetValues(typeof(MatchMode)))
            {
                _db.KeyDelete(RedisKeys.MatchQueueKey(mode));
                _db.KeyDelete(RedisKeys.MatchScoreKey(mode));
            }
        }

        #region Match User
        public async Task<bool> AddMatchUserAsync(MatchMode mode, MatchQueueItem user, int reqTime)
        {
            if (await _db.HashExistsAsync(RedisKeys.MatchUserKey(mode, user.Id), "id"))
            {
                return false;
            }

            try
            {
                var key = RedisKeys.MatchUserKey(mode, user.Id);

                var values = new HashEntry[]
                {
                    new HashEntry("id", user.Id),
                    new HashEntry("mmr", user.MMR),
                    new HashEntry("score", user.Score),
                };

                await _db.HashSetAsync(key, values);
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error adding match user: {ex.Message}");
                throw;
            }

            return true;
        }

        // 유저의 (mmr, score) 정보를 가져옴
        public async Task<(int mmr, long score)> GetMatchUserAsync(MatchMode mode, int id)
        {
            try
            {
                var key = RedisKeys.MatchUserKey(mode, id);

                var mmrTask = _db.HashGetAsync(key, "mmr");
                var scoreTask = _db.HashGetAsync(key, "score");

                await Task.WhenAll(mmrTask, scoreTask);

                int mmr = mmrTask.Result.IsNull ? 0 : (int)mmrTask.Result;
                long score = scoreTask.Result.IsNull ? 0 : (long)scoreTask.Result;

                return (mmr, score);
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error getting match user: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> RemoveMatchUserAsync(MatchMode mode, int id)
        {
            try
            {
                await _db.KeyDeleteAsync(RedisKeys.MatchUserKey(mode, id));
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error removing match user: {ex.Message}");
                throw;
            }

            return true;
        }

        public async Task<bool> RemoveMatchUserAsync(MatchMode mode, ICollection<MatchQueueItem> ids)
        {
            try
            {
                var transaction = _db.CreateTransaction();
                foreach (var id in ids)
                {
                    _ = transaction.KeyDeleteAsync(RedisKeys.MatchUserKey(mode, id.Id));
                }

                return await transaction.ExecuteAsync();
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error removing match user: {ex.Message}");
                throw;
            }
        }


        private void ClearAllMatchUsersAsync()
        {
            try
            {
                foreach (MatchMode mode in Enum.GetValues(typeof(MatchMode)))
                {
                    var server = _connection.GetServer(_connection.GetEndPoints().First());
                    var pattern = $"{RedisKeys.MatchUserKey(mode)}*";
                    var keys = server.Keys(pattern: pattern);

                    foreach (var key in keys)
                    {
                        _db.KeyDelete(key);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error clearing all match users: {ex.Message}");
                throw;
            }
        }
        #endregion

        #region Match Queue
        public async Task<long> GetMatchQueueCountAsync(MatchMode mode)
        {
            try
            {
                return await _db.SortedSetLengthAsync(RedisKeys.MatchQueueKey(mode));
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error getting match queue count: {ex.Message}");
                throw;
            }
        }
            

        public async Task<bool> IsEmptyMatchQueueAsync(MatchMode mode)
        {
            try
            {
                return await _db.SortedSetLengthAsync(RedisKeys.MatchQueueKey(mode)) == 0;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error checking match queue: {ex.Message}");
                throw;
            }
        }
            
        public async Task<List<MatchQueueItem>?> GetUserMatchQueueAsync(MatchMode mode, int begin, int end)
        {
            try
            {
                var sortedSetEntries = await _db.SortedSetRangeByRankWithScoresAsync(RedisKeys.MatchQueueKey(mode), begin, end);
                if (sortedSetEntries == null || sortedSetEntries.Length == 0)
                {
                    return null;
                }

                return sortedSetEntries.Select(i => new MatchQueueItem((int)i.Element).SetScore((long)i.Score)).ToList();
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error getting user match queue: {ex.Message}");
                throw;
            }
        }

        public async Task<long> GetUserScoreMatchQueueAsync(MatchMode mode, int id)
        {
            try
            {
                var score = await _db.SortedSetScoreAsync(RedisKeys.MatchQueueKey(mode), id);
                return (score is null) ? 0 : (long)score;
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error getting user score match queue: {ex.Message}");
                throw;
            }
        }
        #endregion

        #region Match Score
        public async Task<List<MatchQueueItem>?> GetUserMatchScoreAsync(MatchMode mode, long minScore, long maxScore, int count)
        {
            try
            {
                long sortedSetLength = await _db.SortedSetLengthAsync(RedisKeys.MatchScoreKey(mode), minScore, maxScore);
                if (sortedSetLength == 0)
                {
                    return null;
                }

                int offset = _random.Next(0, Math.Max(0, (int)sortedSetLength - count));
                var sortedSetEntries = await _db.SortedSetRangeByScoreWithScoresAsync(
                    RedisKeys.MatchScoreKey(mode), minScore, maxScore, Exclude.None, Order.Ascending, offset, count);
                if (sortedSetEntries is null || sortedSetEntries.Length == 0)
                {
                    return null;
                }

                return sortedSetEntries.Select(i => new MatchQueueItem((int)i.Element).SetMMR((int)i.Score)).ToList();
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error getting match candidates: {ex.Message}");
                throw;
            }
        }
        #endregion

        #region Queue Management
        public async Task<bool> AddQueueAndScoreAsync(MatchMode mode, MatchQueueItem item)
        {
            try
            {
                var transaction = _db.CreateTransaction();
                _ = transaction.SortedSetAddAsync(RedisKeys.MatchQueueKey(mode), item.Id, item.Score);
                _ = transaction.SortedSetAddAsync(RedisKeys.MatchScoreKey(mode), item.Id, item.MMR);

                return await transaction.ExecuteAsync();
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error adding queue and score: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> AddQueueAndScoreAsync(MatchMode mode, IEnumerable<MatchQueueItem> items)
        {
            try
            {
                var transaction = _db.CreateTransaction();
                foreach (var item in items)
                {
                    _ = transaction.SortedSetAddAsync(RedisKeys.MatchQueueKey(mode), item.Id, item.Score);
                    _ = transaction.SortedSetAddAsync(RedisKeys.MatchScoreKey(mode), item.Id, item.MMR);
                }

                return await transaction.ExecuteAsync();
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error adding queue and score: {ex.Message}");
                throw;
            }
        }
        public async Task<bool> RemoveQueueAndScoreAsync(MatchMode mode, int id)
        {
            try
            {
                var transaction = _db.CreateTransaction();
                _ = transaction.SortedSetRemoveAsync(RedisKeys.MatchQueueKey(mode), id);
                _ = transaction.SortedSetRemoveAsync(RedisKeys.MatchScoreKey(mode), id);

                return await transaction.ExecuteAsync();
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error removing queue and score: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> RemoveQueueAndScore(MatchMode mode, IEnumerable<MatchQueueItem> items)
        {
            try
            {
                var transaction = _db.CreateTransaction();
                foreach (var item in items)
                {
                    _ = transaction.SortedSetRemoveAsync(RedisKeys.MatchQueueKey(mode), item.Id);
                    _ = transaction.SortedSetRemoveAsync(RedisKeys.MatchScoreKey(mode), item.Id);
                }

                return await transaction.ExecuteAsync();
            }
            catch (System.Exception ex)
            {
                Console.WriteLine($"Error removing queue and score: {ex.Message}");
                throw;
            }
        }
        #endregion
    }
}
