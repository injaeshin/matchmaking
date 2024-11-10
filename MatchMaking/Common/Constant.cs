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

    public class Constant
    {
        // 큐 대기 만료 시간 (강제 종료)
        public const int WaitTimeout = 60 * 3; // 3min

        public const int FindMatchRetryCount = 3;
        public const int FindQueueBatchSize = 10;

        public const int MinTaskCount = 1;
        public const int MaxTaskCount = 2;

        public const int MatchQueueMinCount = 300;
        public const int TaskProcessWorkingSeconds = 3; // Task 추가 조건
    }
}
