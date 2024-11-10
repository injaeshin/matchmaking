using MatchMaking.Common;

namespace MatchMaking.Redis;

public static class RedisKeys
{
    public static string MatchRequest => "match:request";
    public static string MatchComplete => "match:complete";

    public static string MatchUser => "match:user";
    public static string MatchQueue => "match:queue";
    public static string MatchScore => "match:score";

    public static string MatchQueueKey(MatchMode mode) => $"match:queue:{Converter.ToFastString(mode)}";
    public static string MatchScoreKey(MatchMode mode) => $"match:score:{Converter.ToFastString(mode)}";
    public static string MatchUserKey(MatchMode mode) => $"match:user:{Converter.ToFastString(mode)}";
    public static string MatchUserKey(MatchMode mode, int userId) => $"match:user:{Converter.ToFastString(mode)}:{userId}";
}
