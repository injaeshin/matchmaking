using MatchMaking.Common;
using MatchMaking.Redis;
using MatchMaking.Model;

namespace MatchMaking.Match;

public delegate void OnMatchSuccess(MatchMode mode, Dictionary<int, MatchQueueItem> users);
public delegate void OnMatchFailure(MatchMode mode, MatchQueueItem user);

public class MatchService
{
    private readonly int _maxCount;
    private readonly RedisService _redisService;
    private readonly MatchBalancer _matchBalancer;
    private readonly SimpleLock<int> _lock = new();
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

    public async Task<bool> MatchWorker()
    {
        if (await _redisService.IsEmptyMatchQueueAsync(MatchMode))
        {
            return false;
        }

        var user = await GetUserFromQueueAsync();
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
        if (!await TryFindUserFromScoreAsync(user, targets))
        {
            return false;
        }

        await AssignMatchedUserAsync(targets);

        _pubQueueDecreaseAction(MatchMode, targets.Count);

        return true;
    }

    private async Task AssignMatchedUserAsync(Dictionary<int, MatchQueueItem> targets)
    {
        await _redisService.RemoveMatchUserAsync(MatchMode, targets.Values);

        foreach (var tg in targets.Values)
        {
            _lock.Unlock(tg.Id);
            _matchBalancer.AddMatchTime(tg.WaitTime);
        }

        OnMatchSuccessEvent?.Invoke(MatchMode, targets);
        
    }

    private async Task<bool> TryLockAndRemoveQueueAsync(int id)
    {
        if (!_lock.TryLock(id))
        {
            return false;
        }

        if (!await _redisService.RemoveQueueAndScoreAsync(MatchMode, id))
        {
            Console.WriteLine($"Failed to remove queue and score: {id}");
            
            _lock.Unlock(id);
            return false;
        }

        return true;
    }

    private async Task<int> SelectUserAsync(List<MatchQueueItem> candidates, Dictionary<int, MatchQueueItem> targets)
    {
        foreach (var tg in candidates)
        {
            if (targets.ContainsKey(tg.Id))
            {
                continue;
            }

            if (!await TryLockAndRemoveQueueAsync(tg.Id))
            {
                continue;
            }

            (_, long score) = await _redisService.GetMatchUserAsync(MatchMode, tg.Id);
            tg.SetScore(score);

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

        if (user.WaitTime > Constant.WaitTimeout)
        {
            if (!_lock.Unlock(user.Id))
            {
                throw new Exception($"Failed to remove lock: {user.Id}");
            }

            return false;
        }

        return true;
    }

    private async Task<MatchQueueItem?> GetUserFromQueueAsync()
    {
        int begin = 0;
        int tryCount = 1;
        int batchSize = Constant.FindQueueBatchSize;

        do
        {
            var candidates = await _redisService.GetUserMatchQueueAsync(MatchMode, begin, begin + batchSize - 1);
            if (candidates is null || candidates.Count == 0)
            {
                begin += batchSize;
                continue;
            }

            foreach (var user in candidates)
            {
                if (await TryLockAndRemoveQueueAsync(user.Id))
                {                    
                    return user;
                }
            }

            begin += batchSize;
        } while (++tryCount < Constant.FindMatchRetryCount);

        return null;
    }

    private async Task<bool> TryFindUserFromScoreAsync(MatchQueueItem user, Dictionary<int, MatchQueueItem> targets)
    {
        var tryCount = 1;
        var adjustMMR = _matchBalancer.GetAdjustMMR(user.WaitTime);

        do
        {
            var minScore = Math.Max(0, (user.MMR - adjustMMR) * tryCount);
            var maxScore = Math.Min(9999, (user.MMR + adjustMMR) * tryCount);

            var count = _maxCount - targets.Count - 1;
            var candidates = await _redisService.GetUserMatchScoreAsync(MatchMode, minScore, maxScore, count);
            if (candidates is null || candidates.Count == 0)
            {
                continue;
            }

            if (await SelectUserAsync(candidates, targets) == _maxCount - 1)
            {
                break;
            }
        } while (++tryCount < Constant.FindMatchRetryCount);

        targets.Add(user.Id, user);

        if (targets.Count != _maxCount)
        {
            foreach (var tg in targets.Values)
            {
                await _redisService.AddQueueAndScoreAsync(MatchMode, tg);

                if (!_lock.Unlock(tg.Id))
                {
                    throw new Exception($"Failed to remove user lock: {tg.Id}");
                }
            }

            return false;
        }

        return targets.Count == _maxCount;
    }
}

