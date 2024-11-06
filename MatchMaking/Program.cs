using MatchMaking.Common;
using MatchMaking.Redis;
using MatchMaking.Match;
using MatchMaking.Model;

namespace MatchMaking;

public class Program
{
    static async Task Main(string[] args)
    {
        var mode = MatchMode.ThreeVsThree;

        var redisService = new RedisService();
        var matchManager = new MatchManager(redisService);

        var random = new Random();
        var users = new List<MatchQueueItem>();

        var tasks = Enumerable.Range(0, 1000).Select(i =>
        {
            var mmr = random.Next(1, 100);
            var user = new MatchQueueItem(i, mmr, 0);
            user.SetScore(MatchScore.EncodeScore(user.MMR));
            users.Add(user);
            return Task.CompletedTask;
        });
        await Task.WhenAll(tasks);

        //await redisService._AddQueueAndScoreAsync(MatchMode.ThreeVsThree, users);

        foreach (var u in users)
        {
            await matchManager.AddMatchQueueAsync(mode, u);
        }

        matchManager.Start();

        Console.ReadKey();
    }
}

