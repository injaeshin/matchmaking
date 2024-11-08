using MatchMaking.Common;
using MatchMaking.Redis;
using MatchMaking.Model;

namespace MatchMaking.Match;

public class MatchManager
{
    private Action<MatchMode>? PubIncreaseQueue;

    private readonly RedisService _redisService;
    private readonly TaskCounter<MatchMode> _taskCounter = new();
    private readonly Dictionary<MatchMode, MatchProcessor> _matchProcess = new();

    public MatchManager(RedisService redis)
    {
        _redisService = redis;

        InitEventAction();
        InitMatchProcess();
    }

    #region Initialize
    private void InitMatchProcess()
    {
        foreach (MatchMode mode in Enum.GetValues(typeof(MatchMode)))
        {
            var processor = new MatchProcessor(_redisService, mode);

            var service = processor.MatchService;
            service.OnMatchSuccessEvent += async (mode, users) => await HandleMatchSuccessAsync(mode, users);
            service.OnMatchFailureEvent += async (mode, userId) => await HandleMatchFailureAsync(mode, userId);

            _matchProcess.Add(mode, processor);
        }
    }

    private void InitEventAction()
    {
        PubIncreaseQueue = _redisService.RedisMessage.PubIncreaseMatchQueue;

        _redisService.RedisMessage.IncreaseMatchQueueEvent += async mode => await HandleIncreaseMatchQueueAsync(mode);
        _redisService.RedisMessage.DecreaseMatchQueueEvent += async (mode, matchedUserCount) =>
                                                            await HandleDecreaseMatchQueueAsync(mode, matchedUserCount);
    }
    #endregion

    public void Start()
    {
        foreach (var mp in _matchProcess.Values)
        {
            _taskCounter.IncreaseTask(mp.MatchMode, mp.MatchService, ProcessMatchQueueAsync);
        }
    }

    public void Stop()
    {
        _taskCounter.Clear();
    }

    public async Task<bool> AddMatchQueueAsync(MatchMode mode, MatchQueueItem user)
    {
        if (user.MMR == 0)
        {
            throw new Exception($"Invalid MMR: {user.Id} - {user.MMR}");
        }

        user.SetScore(MatchScore.EncodeScore(user.MMR));

        if (!await _redisService.AddQueueAndScoreAsync(mode, user))
        {
            return false;
        }

        PubIncreaseQueue?.Invoke(mode);
        return true;
    }

    private async Task ProcessMatchQueueAsync(object o, CancellationToken token)
    {
        MatchService service = o as MatchService ?? throw new InvalidOperationException("invalid object");

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!await service.MatchProcess())
                {
                    await Task.Delay(100, token);
                }

                await Task.Delay(30, token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {service.MatchMode} queue: {ex.Message}");
            }
        }
    }

    private async Task HandleMatchSuccessAsync(MatchMode mode, Dictionary<int, MatchQueueItem> users)
    {
        Console.WriteLine($"Match Success: {mode} {string.Join(", ", users.Select(x => x.Key))}");

        await Task.CompletedTask;
    }

    private async Task HandleMatchFailureAsync(MatchMode mode, MatchQueueItem user)
    {
        Console.WriteLine($"Match Failure: {mode} {user.Id}");

        await Task.CompletedTask;
    }

    private async Task HandleIncreaseMatchQueueAsync(MatchMode mode)
    {
        Console.WriteLine($"Match Request: {mode}");

        await Task.CompletedTask;
    }

    private async Task HandleDecreaseMatchQueueAsync(MatchMode mode, int matchedUserCount)
    {
        Console.WriteLine($"Match Complete: {mode}, ");

        await Task.CompletedTask;
    }
}
