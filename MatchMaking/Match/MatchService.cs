using MatchMaking.Common;
using MatchMaking.Redis;
using MatchMaking.Model;

namespace MatchMaking.Match;

public delegate void OnMatchSuccess(MatchMode mode, Dictionary<int, MatchQueueItem> users);
public delegate void OnMatchFailure(MatchMode mode, MatchQueueItem user);

public class MatchService
{
    private const int _tryMaxCount = 3;
    private const int _queueWaitCount = 1000;
    private const int _queueBatchSize = 10;

    private int _maxCount;

    private readonly ConcurrentSet<int> _userLocks = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly RedisService _redisService;
    private readonly MatchBalancer _matchBalancer;
    private readonly Action<MatchMode, int> _pubQueueDecreaseAction;

    public event OnMatchSuccess? OnMatchSuccessEvent;
    public event OnMatchFailure? OnMatchFailureEvent;
    

    public MatchMode MatchMode { get; }

    public MatchService(RedisService redis, MatchBalancer balancer, MatchMode mode)
    {
        MatchMode = mode;
        _maxCount = (int)mode;

        _redisService = redis;
        _matchBalancer = balancer;        
        
        _pubQueueDecreaseAction = _redisService.RedisMessage.PubDecreaseMatchQueue;
    }

    public async Task<bool> MatchProcess()
    {
        if (await _redisService.IsEmptyMatchQueueAsync(MatchMode))
        {
            return false;
        }

        var user = await GetUserQueueAsync();
        if (user is null)
        {
            return false;
        }

        if (!IsValidateMatchOwner(user))
        {
            OnMatchFailureEvent?.Invoke(MatchMode, user);
            return false;
        }

        Dictionary<int, MatchQueueItem> targets = new();
        if (!await TryGetUserMatchAsync(user, targets))
        {
            return false;
        }

        SetMatchedUserAsync(targets);

        return true;
    }

    private void SetMatchedUserAsync(Dictionary<int, MatchQueueItem> targets)
    {
        foreach (var tg in targets.Values)
        {
            if (!_userLocks.Remove(tg.Id))
            {
                throw new InvalidOperationException("Failed to remove lock user");
            }

            _matchBalancer.AddMatchTime(tg.WaitTime);
        }

        OnMatchSuccessEvent?.Invoke(MatchMode, targets);
    }

    private async Task<int> SelectUnlockUserAsync(List<MatchQueueItem> candidates, Dictionary<int, MatchQueueItem> targets)
    {
        foreach (var tg in candidates)
        {
            if (targets.ContainsKey(tg.Id))
            {
                continue;
            }

            if (!_userLocks.Add(tg.Id))
            {
                continue;
            }

            var matchQueueScore = await _redisService.GetUserScoreMatchQueueAsync(MatchMode, tg.Id);
            if (matchQueueScore == 0)
            {
                throw new Exception($"Invalid Match Queue Score: {tg.Id}");
            }

            await _redisService.RemoveQueueAndScoreAsync(MatchMode, tg.Id);

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

        if (user.WaitTime > MatchBalancer.MatchWaitTimeout)
        {
            if (!_userLocks.Remove(user.Id))
            {
                throw new Exception($"Failed to remove lock: {user.Id}");
            }

            return false;
        }

        return true;
    }

    private async Task<bool> TryGetUserMatchAsync(MatchQueueItem user, Dictionary<int, MatchQueueItem> targets)
    {
        var tryCount = 1;
        var adjustMMR = _matchBalancer.GetAdjustMMR(user.WaitTime);

        do
        {
            var minScore = Math.Max(0, (user.MMR - adjustMMR) * tryCount);
            var maxScore = Math.Min(9999, (user.MMR + adjustMMR) * tryCount);

            var count = _maxCount - targets.Count - 1;
            var candidates = await _redisService.GetMatchCandidatesAsync(MatchMode, minScore, maxScore, count);
            if (candidates is null || candidates.Count == 0)
            {
                break;
            }

            if (await SelectUnlockUserAsync(candidates, targets) == _maxCount -1)
            {
                break;
            }
        } while (++tryCount < _tryMaxCount);

        targets.Add(user.Id, user);

        if (targets.Count != _maxCount)
        {
            foreach (var tg in targets.Values)
            {
                await _redisService.AddQueueAndScoreAsync(MatchMode, tg);

                if (!_userLocks.Remove(tg.Id))
                {
                    throw new Exception($"Failed to remove user lock: {tg.Id}");
                }
            }

            targets.Clear();
            return false;
        }

        return targets.Count == _maxCount;
    }

    public async Task<MatchQueueItem?> GetUserQueueAsync()
    {
        int begin = 0;
        int retryCount = _tryMaxCount;

        do
        {
            var candidates = await _redisService.GetUserMatchQueueAsync(MatchMode, begin, begin + _queueBatchSize - 1);
            if (candidates is null || candidates.Count == 0)
            {
                return null;
            }

            foreach (var candidate in candidates)
            {
                var id = (int)candidate.Id;

                if (!_userLocks.Add(id))
                {
                    continue;
                }

                if (!await _redisService.RemoveQueueAndScoreAsync(MatchMode, id))
                {
                    Console.WriteLine($"Failed to remove queue and score: {id}");
                }

                return new MatchQueueItem(id, 0, (long)candidate.Score);
            }

            begin += _queueBatchSize;
        } while (--retryCount > 0);

        return null;
    }

    //public async void MatchWoker(CancellationToken token)
    //{
    //    while (!token.IsCancellationRequested)
    //    {
    //        var waitingCount = await _redisService.GetMatchQueueCount(_mode);
    //        if (waitingCount == 0)
    //        {
    //            await Task.Delay(100);
    //            continue;
    //        }
    //        if (waitingCount <= _queueWaitCount)
    //        {
    //            await _semaphore.WaitAsync();
    //            try
    //            {
    //                if (!await MatchProcess())
    //                {
    //                    await Task.Delay(30);
    //                }
    //            }
    //            finally
    //            {
    //                _semaphore.Release();
    //            }
    //        }
    //        else
    //        {
    //            if (!await MatchProcess())
    //            {
    //                await Task.Delay(30);
    //            }
    //        }
    //        await Task.Delay(30);
    //    }
    //}
}

