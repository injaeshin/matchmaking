using MatchMaking.Common;
using MatchMaking.Data;
using MatchMaking.Model;
using System.Diagnostics;

namespace MatchMaking.Match;

public class MatchService : IDisposable
{
    private const int _tryMaxCount = 3;
    private const int _matchTimeout = 60; // seconds
    private const int _requiredWaitCount = 50;
    private const int _queueBatchSize = 5;

    private bool _isRunning = false;
    private bool _disposed = false;


    private readonly RedisService _redisService;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    private readonly List<Task> _workerTasks = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    // test code
    private int _matchCount = 0;
    private Stopwatch _queueStopwatch = new Stopwatch();

    public MatchService(RedisService redisService)
    {
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
            if (waitingCount <= _requiredWaitCount)
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
            if (await _redisService.IsEmptyMatchQueue())
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
        if (await _redisService.IsEmptyMatchQueue())
        {
            return false;
        }

        var matchOwner = await GetUserQueueAsync();
        if (matchOwner == null)
        {
            return false;
        }

        if (matchOwner.MMR <= 0)
        {
            throw new Exception($"Invalid MMR: {matchOwner.Id} - {matchOwner.MMR}");
        }

        //if (matchOwner.WaitTime > _matchTimeout)
        //{
        //    Console.WriteLine($"waitTime > _matchTimeout: {matchOwner.Id}");
        //    await DelQueueAndScore(matchOwner.Id);
        //    return false;
        //}

        Dictionary<int, MatchQueueItem> matchUsers = new();
        if (!await TryGetMatchUser(matchOwner, matchUsers))
        {
            return false;
        }

        foreach (var user in matchUsers.Values)
        {
            if (!await _redisService.RemoveLock(user.Id))
            {
                throw new Exception($"Failed to remove lock: {user.Id}");
            }

            Interlocked.Increment(ref _matchCount);
        }

        Console.WriteLine($"Match Success: {string.Join(", ", matchUsers.Keys)}");
        return true;
    }

    private async Task<bool> TryGetMatchUser(MatchQueueItem matchOwner, Dictionary<int, MatchQueueItem> users)
    {
        var maxCount = (int)MatchMode.FiveVsFive;
        users.Add(matchOwner.Id, matchOwner);
       
        var tryCount = 1;
        var adjustScore = Score.GetAdjustScore(matchOwner.MMR, matchOwner.WaitTime);

        do
        {
            if (users.Count == maxCount)
            {
                break;
            }

            var minScore = Math.Max(0, (matchOwner.MMR - adjustScore) * tryCount);
            var maxScore = Math.Min(9999, (matchOwner.MMR + adjustScore) * tryCount);

            var candidates = await _redisService.GetMatchCandidates(minScore, maxScore, maxCount - users.Count);
            if (candidates == null || candidates.Count == 0)
            {
                break;
            }

            foreach (var usr in candidates)
            {
                if (users.ContainsKey(usr.Id) || await _redisService.IsLock(usr.Id))
                {
                    continue;
                }

                if (await _redisService.AddLockAndRemoveQueue(usr.Id))
                {
                    usr.SetDecodeScore(await _redisService.GetUserScoreMatchQueue(usr.Id));
                    users.Add(usr.Id, usr);
                }
            }
        } while (++tryCount < _tryMaxCount);

        // Re-add unmatched users to the queue if not enough users were found
        if (users.Count != maxCount)
        {
            foreach (var user in users.Values)
            {
                if (!await _redisService.RemoveLockAndAddQueue(user))
                {
                    throw new Exception($"Failed to add lock: {user.Id}");
                }
            }

            users.Clear();
            return false;
        }

        return true;
    }

    public async Task<bool> AddMatchQueue(MatchQueueItem user)
    {
        user.SetScore(Score.EncodeScore(user.MMR));

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
                if (await _redisService.AddLockAndRemoveQueue(id))
                {
                    var matchOwner = new MatchQueueItem(id, 0, (long)candidate.Score);
                    return matchOwner;
                }
            }

            begin += _queueBatchSize;
        } while (--retryCount > 0);

        return null;
    }
}

