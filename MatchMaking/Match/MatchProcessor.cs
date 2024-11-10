using MatchMaking.Common;
using MatchMaking.Redis;

namespace MatchMaking.Match;

public class MatchProcessor
{   

    public MatchMode MatchMode { get; }
    public MatchService MatchService { get; }
    public MatchBalancer MatchBalancer { get; } = new();

    public MatchProcessor(RedisService redis, MatchMode mode)
    {
        MatchMode = mode;
        MatchService = new MatchService(redis, MatchBalancer, mode);
    }
}