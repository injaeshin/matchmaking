namespace MatchMaking.Common
{
    public enum MatchStatus
    {
        None,
        Waiting,
        Pending,
        Completed
    }

    public enum MatchMode
    {
        None = 0,
        OneVsOne = 2,
        TwoVsTwo = 4,
        ThreeVsThree = 6,
        FiveVsFive = 10
    }

    public enum LockType
    {
        None = 0,
        Local = 1,
        Redis = 2,
        Both = 3
    }
}
