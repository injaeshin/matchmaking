namespace MatchMaking.Common;

public class Util
{
    public static long GetUnixTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}