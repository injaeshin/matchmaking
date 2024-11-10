using MatchMaking.Common;
using MatchMaking.Redis;
using MatchMaking.Model;

namespace MatchMaking.Match;

public partial class MatchManager
{
    private Action<MatchMode>? _pubIncreaseQueue;

    private readonly RedisService _redisService;
    private readonly TaskCounter<MatchMode> _taskCounter = new();
    private readonly Dictionary<MatchMode, MatchProcessor> _matchProcess = new();
    private readonly List<Timer> checkTimers = new();

    public MatchManager(RedisService redis)
    {
        _redisService = redis;

        InitTimer();
        InitEventAction();
        InitMatchProcess();        
    }

    ~MatchManager()
    {
        _taskCounter.Dispose();

        foreach (var t in checkTimers)
        {
            t?.Change(Timeout.Infinite, 0);
            t?.Dispose();
        }
    }

    private async Task IncreaseTaskWithQueueAsync()
    {
        foreach (var processor in _matchProcess.Values)
        {
            var mode = processor.MatchMode;
            if (_taskCounter.GetTaskCount(mode) >= Constant.MaxTaskCount)
            {
                continue;
            }

            if (processor.MatchBalancer.GetAverageMatchTime() < Constant.TaskProcessWorkingSeconds)
            {
                continue;
            }

            if (await _redisService.GetMatchQueueCountAsync(mode) <= Constant.MatchQueueMinCount)
            {
                continue;
            }

            await _taskCounter.IncreaseTask(mode, ProcessMatchQueueAsync, processor.MatchService);
        }
    }

    private async Task DecreaseTaskWithQueueAsync()
    {
        foreach (var processor in _matchProcess.Values)
        {
            var mode = processor.MatchMode;
            if (_taskCounter.GetTaskCount(mode) <= Constant.MinTaskCount)
            {
                continue;
            }

            if (await _redisService.GetMatchQueueCountAsync(mode) > Constant.MatchQueueMinCount)
            {
                continue;
            }

            Console.WriteLine($"DecreaseTask: {mode} - {await _redisService.GetMatchQueueCountAsync(mode)}");

            await _taskCounter.DecreaseTask(mode);
        }
    }

    #region Initialize
    private void InitTimer()
    {
        var averageTimerCheck = new Timer(async _ => await IncreaseTaskWithQueueAsync(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3));
        var queueStatusCheck = new Timer(async _ => await DecreaseTaskWithQueueAsync(), null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3));

        checkTimers.Add(averageTimerCheck);
        checkTimers.Add(queueStatusCheck);
    }

    private void InitEventAction()
    {
        _pubIncreaseQueue = _redisService.RedisMessage.PubIncreaseMatchQueue;

        _redisService.RedisMessage.IncreaseMatchQueueEvent += async mode => await HandleIncreaseMatchQueueAsync(mode);
        _redisService.RedisMessage.DecreaseMatchQueueEvent += async (mode, matchedUserCount) =>
                                                            await HandleDecreaseMatchQueueAsync(mode, matchedUserCount);
    }

    private void InitMatchProcess()
    {
        foreach (MatchMode mode in Enum.GetValues(typeof(MatchMode)))
        {
            if (mode == MatchMode.None)
            {
                continue;
            }

            var processor = new MatchProcessor(_redisService, mode);

            var service = processor.MatchService;
            service.OnMatchSuccessEvent += async (mode, users) => await HandleMatchSuccessAsync(mode, users);
            service.OnMatchFailureEvent += async (mode, userId) => await HandleMatchFailureAsync(mode, userId);

            _matchProcess.Add(mode, processor);
        }
    }
    #endregion

    public void Start()
    {
        foreach (var mp in _matchProcess.Values)
        {
            if (mp.MatchMode != MatchMode.ThreeVsThree)
            {
                continue;
            }

            _taskCounter.IncreaseTask(mp.MatchMode, ProcessMatchQueueAsync, mp.MatchService).GetAwaiter();
        }
    }

    public async Task<bool> AddMatchQueueAsync(MatchMode mode, MatchQueueItem user)
    {
        if (user.MMR == 0)
        {
            throw new Exception($"Invalid MMR: {user.Id} - {user.MMR}");
        }

        user.SetScore(MatchScore.EncodeScore(user.MMR));

        if (!await _redisService.AddMatchUserAsync(mode, user, 0))
        {
            return false;
        }

        if (!await _redisService.AddQueueAndScoreAsync(mode, user))
        {
            await _redisService.RemoveMatchUserAsync(mode, user.Id);
            return false;
        }

        _pubIncreaseQueue?.Invoke(mode);
        return true;
    }

    private async Task ProcessMatchQueueAsync(object o, CancellationToken token)
    {
        MatchService service = o as MatchService ?? throw new InvalidOperationException("Invalid object");

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!await service.MatchWorker())
                {
                    await Task.Delay(100, token);
                }

                await Task.Delay(30, token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Task was cancelled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {service.MatchMode} queue: {ex.Message}");
            }
        }
    }
}

public partial class MatchManager
{
    private async Task HandleMatchSuccessAsync(MatchMode mode, Dictionary<int, MatchQueueItem> users)
    {
        //Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Match Success: {mode} {string.Join(", ", users.Keys)}");

        await Task.CompletedTask;
    }

    private async Task HandleMatchFailureAsync(MatchMode mode, MatchQueueItem user)
    {
        Console.WriteLine($"Match Failure: {mode} {user.Id}");

        await Task.CompletedTask;
    }

    private async Task HandleIncreaseMatchQueueAsync(MatchMode mode)
    {
        //Console.WriteLine($"Match Request: {mode}");

        await Task.CompletedTask;
    }

    private async Task HandleDecreaseMatchQueueAsync(MatchMode mode, int matchedUserCount)
    {
        //Console.WriteLine($"Match Complete: {mode}, ");

        await DecreaseTaskWithQueueAsync();

        await Task.CompletedTask;
    }
}