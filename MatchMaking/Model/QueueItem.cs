
using RedLockNet;

namespace MatchMaking.Model;

public class MatchQueueItem
{
    private int _id;
    private int _mmr;
    private int _waitTime;
    private long _score;
    private IRedLock? _lock = null;

    public MatchQueueItem(int id, int mmr, long score, IRedLock? redLock = null)
    {
        _id = id;
        _mmr = mmr;

        if (score > 0)
        {
            SetDecodeScore(score);
        }

        _lock = redLock;
    }

    public int Id => _id;
    public int MMR => _mmr;
    public long Score => _score;
    public int WaitTime => _waitTime;

    public MatchQueueItem SetMMR(int mmr)
    {
        _mmr = mmr;
        return this;
    }

    public MatchQueueItem SetScore(long score)
    {
        _score = score;
        return this;
    }
    public MatchQueueItem SetWaitTime(int time)
    {
        _waitTime = time;
        return this;
    }

    public MatchQueueItem SetLock(IRedLock? @lock)
    {
        _lock = @lock;
        return this;
    }
    public IRedLock? GetLock()
    {
        return _lock;
    }

    public MatchQueueItem SetDecodeScore(long score)
    {
        _score = score;
        (var mmr, int waitTime) = Common.Score.DecodeScore(score);
        _mmr = mmr;
        _waitTime = waitTime;

        return this;
    }
}