using MatchMaking.Common;

namespace MatchMaking.Match
{
    public readonly struct MatchProcessor(MatchMode mode, MatchService matchService, CancellationTokenSource cancellationTokenSource)
    {
        public readonly MatchMode Mode { get; } = mode;
        public readonly MatchService MatchService { get; } = matchService;
        public readonly CancellationTokenSource CancellationToken { get; } = cancellationTokenSource;
    }
}
