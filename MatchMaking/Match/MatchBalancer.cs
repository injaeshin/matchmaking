namespace MatchMaking.Match;

public class MatchBalancer
{
    private const int MATCH_WAIT_TIMEOUT   = 60; // seconds
    private const int WAIT_TIME_WEIGHT     = 1000;        
    private const int MATCH_TIME_MAX_COUNT = 300;
    
    public static int MatchWaitTimeout => MATCH_WAIT_TIMEOUT;

    private readonly object _lock = new();
    private readonly int[] _matchTimes = new int[MATCH_TIME_MAX_COUNT];

    private int _currentIndex = 0;
    private int _totalSeconds = 0;

    public int GetAverageMatchTime()
    {
        lock (_lock)
        {
            if (_totalSeconds == 0)
            {
                return 0;
            }

            return _totalSeconds / MATCH_TIME_MAX_COUNT;
        }
    }

    public void AddMatchTime(int seconds)
    {
        lock (_lock)
        {
            _totalSeconds -= _matchTimes[_currentIndex];
            _matchTimes[_currentIndex] = seconds;
            _totalSeconds += seconds;
            _currentIndex = (_currentIndex + 1) % MATCH_TIME_MAX_COUNT;
        }
    }

    public int GetAdjustMMR(int waitTime)
    {
        double averageMatchTime = GetAverageMatchTime();

        // 전체 대기 시간에 따라 가중치에서 10초까지 10%, 30초까지 20%, 그외 30% 적용
        double processWaitWeight = averageMatchTime switch
        {
            > 10 and <= 30 => (double)(WAIT_TIME_WEIGHT * 0.1), // 100
            > 30 and <= 60 => (double)(WAIT_TIME_WEIGHT * 0.2), // 200
            _ => (double)(WAIT_TIME_WEIGHT * 0.25),             // 250
        };

        // 사용자 대기 시간에 따라 가중치 5초까지 20%, 15초까지 30%, 30초까지 40%, 그외 50% 적용
        double waitTimeWeight = waitTime switch
        {
            > 05 and <= 15 => (double)(WAIT_TIME_WEIGHT * 0.1), // 100
            > 15 and <= 30 => (double)(WAIT_TIME_WEIGHT * 0.2), // 200
            > 30 and <= 40 => (double)(WAIT_TIME_WEIGHT * 0.3), // 300
            _ => (double)(WAIT_TIME_WEIGHT * 0.4),              // 400
        };

        return (int)(processWaitWeight + waitTimeWeight);
    }
}