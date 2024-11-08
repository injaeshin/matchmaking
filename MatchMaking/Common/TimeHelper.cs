namespace MatchMaking.Common;

public class TimeHelper
{
    public static long GetUnixTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}