using MatchMaking.Common;
using MatchMaking.Data;
using MatchMaking.Model;

using System.Diagnostics;

namespace MatchMaking.Match;

public class MatchService : IDisposable
{
    private const int _tryMaxCount = 3;
    private const int _queueWaitCount = 1000;
    private const int _queueBatchSize = 10;

    private bool _isRunning = false;
    private bool _disposed = false;

    private readonly RedisService _redisService;
    private readonly MatchBalancer _matchBalancer;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    private readonly List<Task> _workerTasks = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    // test code
    private int _matchCount = 0;
    private Stopwatch _queueStopwatch = new Stopwatch();

    private ConcurrentSet<int> _lockUsers = new();

    public MatchService(RedisService redisService)
    {
        _matchBalancer = new();
        _redisService = redisService;

        _workerTasks.Add(Task.Run(() => MatchWoker()));
        _workerTasks.Add(Task.Run(() => MatchWoker()));
    }

    public void Start()
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        _queueStopwatch.Start();
    }

    public void Stop()
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;
        _cancellationTokenSource.Cancel();
        Task.WhenAll(_workerTasks).GetAwaiter();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Stop();
            _cancellationTokenSource.Dispose();
        }

        _disposed = true;
    }


    public async void MatchWoker()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            if (!_isRunning)
            {
                await Task.Delay(100);
                continue;
            }

            var waitingCount = await _redisService.GetMatchQueueCount();
            if (waitingCount == 0)
            {
                await Task.Delay(100);
                continue;
            }

            // Single-threaded processing if queue count is below required wait count
            if (waitingCount <= _queueWaitCount)
            {
                await _semaphore.WaitAsync();
                try
                {
                    if (!await MatchProcess())
                    {
                        await Task.Delay(30);
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            else
            {
                if (!await MatchProcess())
                {
                    await Task.Delay(30);
                }
            }

            // Log time and count when queue is empty
            if (await _redisService.IsEmptyMatchQueueAsync())
            {
                _queueStopwatch.Stop();
                Console.WriteLine($"큐가 비워질 때까지의 시간: {_queueStopwatch.ElapsedMilliseconds} ms");
                Console.WriteLine($"매칭 성공 유저: {_matchCount}");
                _queueStopwatch.Reset();
                _queueStopwatch.Start();
            }

            await Task.Delay(30);
        }
    }

    // Processes matching for a single session
    public async Task<bool> MatchProcess()
    {
        if (await _redisService.IsEmptyMatchQueueAsync())
        {
            return false;
        }

        var user = await GetUserQueueAsync();
        if (user == null)
        {
            return false;
        }

        if (!IsValidateMatchOwner(user))
        {
            return false;
        }

        Dictionary<int, MatchQueueItem> targets = new();
        if (!await TryGetUserMatchAsync(user, targets))
        {
            return false;
        }

        SetMatchedUserAsync(targets);

        Console.WriteLine($"Match Success: {string.Join(", ", targets.Keys)}");
        return true;
    }

    private void SetMatchedUserAsync(Dictionary<int, MatchQueueItem> targets)
    {
        foreach (var tg in targets.Values)
        {
            if (!_lockUsers.Remove(tg.Id))
            {
                throw new InvalidOperationException("Failed to remove lock user");
            }

            //if (!await _redisService.RemoveLock(tg.Id))
            //{
            //    throw new Exception($"Failed to remove lock: {tg.Id}");
            //}

            _matchBalancer.AddMatchTime(tg.WaitTime);

            Interlocked.Increment(ref _matchCount);
        }
    }

    private async Task<int> SelectUnlockUserAsync(List<MatchQueueItem> candidates, Dictionary<int, MatchQueueItem> targets)
    {
        foreach (var tg in candidates)
        {
            if (targets.ContainsKey(tg.Id))
            {
                continue;
            }

            if (!_lockUsers.Add(tg.Id))
            {
                continue;
            }

            //if (!await _redisService.AddLock(tg.Id))
            //{
            //    Console.WriteLine($"Failed to add lock: {tg.Id}");
            //    continue;
            //}

            var matchQueueScore = await _redisService.GetUserScoreMatchQueue(tg.Id);
            if (matchQueueScore == 0)
            {
                throw new Exception($"Invalid Match Queue Score: {tg.Id}");
            }

            await _redisService.RemoveQueueAndScore(tg.Id);

            tg.SetDecodeScore(matchQueueScore);
            targets.Add(tg.Id, tg);
        }

        return targets.Count;
    }

    private bool IsValidateMatchOwner(MatchQueueItem user)
    {
        if (user.MMR <= 0)
        {
            throw new Exception($"Invalid MMR: {user.Id} - {user.MMR}");
        }

        //if (user.WaitTime > MatchBalancer.RequestTimeout)
        //{
        //    Console.WriteLine($"Request Timeout: {user.Id} - {user.WaitTime}");
        //    //if (!await _redisService.RemoveLock(user.Id))
        //    //{
        //    //    throw new Exception($"Failed to remove lock: {user.Id}");
        //    //}

        //    return false;
        //}

        return true;
    }

    private async Task<bool> TryGetUserMatchAsync(MatchQueueItem user, Dictionary<int, MatchQueueItem> targets)
    {
        var maxCount = (int)MatchMode.FiveVsFive;
       
        var tryCount = 1;
        var adjustMMR = _matchBalancer.GetAdjustMMR(user.WaitTime);

        do
        {
            var minScore = Math.Max(0, (user.MMR - adjustMMR) * tryCount);
            var maxScore = Math.Min(9999, (user.MMR + adjustMMR) * tryCount);

            var count = maxCount - targets.Count - 1;
            var candidates = await _redisService.GetMatchCandidates(minScore, maxScore, count);
            if (candidates == null || candidates.Count == 0)
            {
                break;
            }

            if (await SelectUnlockUserAsync(candidates, targets) == maxCount -1)
            {
                break;
            }
        } while (++tryCount < _tryMaxCount);

        targets.Add(user.Id, user);

        // Re-add unmatched users to the queue if not enough users were found
        if (targets.Count != maxCount)
        {
            foreach (var tg in targets.Values)
            {
                await _redisService.AddQueueAndScore(tg);

                //if (!await _redisService.RemoveLock(tg.Id))
                //{
                //    throw new Exception($"Failed to remove lock: {tg.Id}");
                //}

                if (!_lockUsers.Remove(tg.Id))
                {
                    throw new Exception($"Failed to remove user lock: {tg.Id}");
                }
            }

            targets.Clear();
            return false;
        }

        return targets.Count == maxCount;
    }

    public async Task<bool> AddMatchQueueAsync(MatchQueueItem user)
    {
        user.SetScore(Score.EncodeScore(user.MMR));
        if (user.MMR == 0)
        {
            throw new Exception($"Invalid MMR: {user.Id} - {user.MMR}");
        }

        if (!await _redisService.AddMatchQueue(user))
        {
            return false;
        }

        if (!await _redisService.AddMatchScore(user))
        {
            await _redisService.RemoveMatchScore(user.Id);
            return false;
        }

        return true;
    }

    // Retrieves the next user from the queue
    public async Task<MatchQueueItem?> GetUserQueueAsync()
    {
        int begin = 0;
        int retryCount = _tryMaxCount;

        do
        {
            var candidates = await _redisService.GetUserMatchQueue(begin, begin + _queueBatchSize - 1);
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            foreach (var candidate in candidates)
            {
                var id = (int)candidate.Id;

                if (!_lockUsers.Add(id))
                {
                    continue;
                }

                //if (!await _redisService.AddLock(id))
                //{
                //    Console.WriteLine($"Failed to add lock: {id}");
                //}

                if (!await _redisService.RemoveQueueAndScore(id))
                {
                    Console.WriteLine($"Failed to remove queue and score: {id}");
                }

                return new MatchQueueItem(id, 0, (long)candidate.Score);
            }

            begin += _queueBatchSize;
        } while (--retryCount > 0);

        return null;
    }
}

