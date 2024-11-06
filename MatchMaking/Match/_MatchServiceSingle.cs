using MatchMaking.Common;
using MatchMaking.Redis;
using MatchMaking.Model;

using System.Diagnostics;

namespace MatchMaking.Match;

public class MatchServiceSingle
{
    private const int _tryMaxCount = 3;    
    private const int _queueBatchSize = 1;

    private bool _isRunning = false;
    private bool _disposed = false;

    private int _maxCount;
    private MatchMode _mode = MatchMode.ThreeVsThree;
    private readonly RedisService _redisService;
    private readonly MatchBalancer _matchBalancer;

    private readonly Task _worker;
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private ConcurrentSet<int> _cancelUser = new();

    private int _matchCount = 0;
    private Stopwatch _queueStopwatch = new Stopwatch();

    public MatchServiceSingle(RedisService redisService)
    {
        _maxCount = (int)_mode;

        _matchBalancer = new();
        _redisService = redisService;

        _worker = Task.Run(() => MatchWoker());
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
        _worker.GetAwaiter();
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

            var waitingCount = await _redisService.GetMatchQueueCountAsync(_mode);
            if (waitingCount == 0)
            {
                await Task.Delay(100);
                continue;
            }

            if (!await MatchProcess())
            {
                await Task.Delay(30);
            }

            // Log time and count when queue is empty
            if (await _redisService.IsEmptyMatchQueueAsync(_mode))
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
        if (await _redisService.IsEmptyMatchQueueAsync(_mode))
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

        await AssignMatchedUsers(targets);

        Console.WriteLine($"Match Success: {string.Join(", ", targets.Keys)}");
        return true;
    }

    private async Task AssignMatchedUsers(Dictionary<int, MatchQueueItem> targets)
    {
        var users = targets.Values.ToList();
        await _redisService._RemoveQueueAndScore(_mode, users);

        foreach (var u in users)
        {
            _matchBalancer.AddMatchTime(u.WaitTime);

            _matchCount++;
        }
    }

    private void SelectUnlockUserAsync(List<MatchQueueItem> candidates, Dictionary<int, MatchQueueItem> targets, int count)
    {
        foreach (var tg in candidates)
        {
            if (targets.ContainsKey(tg.Id))
            {
                continue;
            }

            targets.Add(tg.Id, tg);

            if (targets.Count == count)
            {
                break;
            }
        }
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
        var tryCount = 1;
        var adjustMMR = _matchBalancer.GetAdjustMMR(user.WaitTime);

        targets.Add(user.Id, user);

        do
        {
            var minScore = Math.Max(0, (user.MMR - adjustMMR) * tryCount);
            var maxScore = Math.Min(9999, (user.MMR + adjustMMR) * tryCount);

            var count = _maxCount - targets.Count + 10;
            var candidates = await _redisService.GetMatchCandidatesAsync(_mode, minScore, maxScore, count);
            if (candidates == null || candidates.Count == 0)
            {
                break;
            }

            SelectUnlockUserAsync(candidates, targets, _maxCount);
            if (targets.Count == _maxCount)
            {
                break;
            }
        } while (++tryCount < _tryMaxCount);

        // Re-add unmatched users to the queue if not enough users were found
        if (targets.Count != _maxCount)
        {
            targets.Clear();
            return false;
        }

        return targets.Count == _maxCount;
    }

    public async Task<bool> AddMatchQueueAsync(MatchQueueItem user)
    {
        user.SetScore(MatchScore.EncodeScore(user.MMR));
        if (user.MMR == 0)
        {
            throw new Exception($"Invalid MMR: {user.Id} - {user.MMR}");
        }

        if (!await _redisService.AddMatchQueueAsync(_mode, user))
        {
            return false;
        }

        if (!await _redisService.AddMatchScoreAsync(_mode, user))
        {
            await _redisService.RemoveMatchScoreAsync(_mode, user.Id);
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
            var candidates = await _redisService.GetUserMatchQueueAsync(_mode, begin, begin + _queueBatchSize - 1);
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            foreach (var candidate in candidates)
            {
                var id = (int)candidate.Id;

                if (_cancelUser.Contains(id))
                {
                    _cancelUser.Remove(id);
                    continue;
                }

                return new MatchQueueItem(id, 0, (long)candidate.Score);
            }

            begin += _queueBatchSize;
        } while (--retryCount > 0);

        return null;
    }
}

