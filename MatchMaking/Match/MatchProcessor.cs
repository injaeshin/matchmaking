using MatchMaking.Common;
using MatchMaking.Redis;

namespace MatchMaking.Match
{
    public readonly struct MatchProcessor
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
}
