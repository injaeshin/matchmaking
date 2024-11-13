using MatchMaking.Common;

namespace MatchMaking.Match;

public class MatchBalancer
{
    private const int WaitTimeReset = 5;
    private const int WaitTimeWeight = 1000;
    private const int WaitTimeMaxCount = 50;

    private readonly object _lock = new();
    private readonly int[] _matchTimes = new int[WaitTimeMaxCount];
        
    private int _currentIndex = 0;
    private int _totalSeconds = 0;
    private long _lastAddedMatchTime = 0;

    public int GetAverageMatchTime()
    {
        lock (_lock)
        {
            if (_totalSeconds == 0)
            {
                return 0;
            }

            if ((TimeHelper.GetUnixTimestamp() - _lastAddedMatchTime) > WaitTimeReset)
            {
                ResetMatchTimes();
            }

            int actualCount = _matchTimes.Count(t => t > 0);
            return actualCount == 0 ? 0 : _totalSeconds / actualCount;
        }
    }

    private void ResetMatchTimes()
    {
        _totalSeconds = 0;
        _currentIndex = 0;
        _lastAddedMatchTime = 0;
        Array.Clear(_matchTimes, 0, _matchTimes.Length);
    }

    public void AddMatchTime(int seconds)
    {
        lock (_lock)
        {
            _totalSeconds -= _matchTimes[_currentIndex];
            _matchTimes[_currentIndex] = seconds;
            _totalSeconds += seconds;
            _currentIndex = (_currentIndex + 1) % WaitTimeMaxCount;
            _lastAddedMatchTime = TimeHelper.GetUnixTimestamp();
        }
    }

    public int GetAdjustMMR(int waitTime)
    {
        double averageMatchTime = GetAverageMatchTime();

        // 전체 대기 시간에 따라 가중치에서 30초까지 10%, 60초까지 20% 그외 25% 적용
        double processWaitWeight = averageMatchTime switch
        {
            >= 0 and <= 30 => (double)(WaitTimeWeight * 0.1), // 100
            > 30 and <= 60 => (double)(WaitTimeWeight * 0.2), // 200
            _ => (double)(WaitTimeWeight * 0.25),             // 250
        };

        // 사용자 대기 시간에 따라 가중치 15초까지 10%, 30초까지 20%, 40초까지 30%, 그외 40% 적용
        double waitTimeWeight = waitTime switch
        {
            >= 0 and <= 15 => (double)(WaitTimeWeight * 0.1), // 100
            > 15 and <= 30 => (double)(WaitTimeWeight * 0.2), // 200
            > 30 and <= 40 => (double)(WaitTimeWeight * 0.3), // 300
            _ => (double)(WaitTimeWeight * 0.4),              // 400
        };

        return (int)(processWaitWeight + waitTimeWeight);
    }
}