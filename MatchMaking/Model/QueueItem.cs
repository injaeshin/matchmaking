using MatchMaking.Common;

namespace MatchMaking.Model;

public class MatchQueueItem
{
    private int _id;
    private int _mmr;
    private long _score;
    private int  _waitTime;

    public int Id => _id;
    public int MMR => _mmr;
    public long Score => _score;
    public int WaitTime => _waitTime;

    public MatchQueueItem(int id)
    {
        _id = id;
    }

    public MatchQueueItem SetMMR(int mmr)
    {
        _mmr = mmr;
        return this;
    }

    public MatchQueueItem SetScore(long score)
    {
        _score = score;
        (_mmr, _waitTime) = Match.MatchScore.DecodeScore(score);
        return this;
    }
}