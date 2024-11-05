namespace MatchMaking.Common;

public class Score
{
    private const int MIN_MMR = 0;
    private const int MAX_MMR = 9999;
    private const int MMR_MULTIPLIER = 10000;         // MMR 자릿수를 위한 상수

    public static long EncodeScore(int mmr)
    {
        var ts = Util.GetUnixTimestamp();
        // timestamp * 10000 + mmr
        // 예: 1500 MMR, 1000초 → 10000000 + 1500 = 10001500
        return ts * MMR_MULTIPLIER + mmr;
    }

    public static (int mmr, int waitTime) DecodeScore(long score)
    {
        // 하위 4자리는 MMR
        var mmr = (int)(score % MMR_MULTIPLIER);
        if (mmr < MIN_MMR || mmr > MAX_MMR)
        {
            return (0, 0);
        }

        // 상위는 등록 시간(초)
        var beginTime = score / MMR_MULTIPLIER;
        var waitTime = (int)(Util.GetUnixTimestamp() - beginTime);

        return (mmr, waitTime);
    }
}
