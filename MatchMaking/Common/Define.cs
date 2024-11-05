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
        TwoVsTwo = 4,
        ThreeVsThree = 6,
        FourVsFour = 8,
        FiveVsFive = 10
    }
}
