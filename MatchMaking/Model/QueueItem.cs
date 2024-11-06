namespace MatchMaking.Model;

public class MatchQueueItem
{
    private readonly int _id;
    private long _score;

    private int _mmr;    
    private int  _waitTime;

    public MatchQueueItem(int id, int mmr, long score)
    {
        _id = id;
        _mmr = mmr;

        if (score > 0)
        {
            SetDecodeScore(score);
        }
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

    public MatchQueueItem SetDecodeScore(long score)
    {
        _score = score;
        (var mmr, int waitTime) = Match.MatchScore.DecodeScore(score);
        _mmr = mmr;
        _waitTime = waitTime;
        return this;
    }
}