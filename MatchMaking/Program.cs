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

        var redisService = new RedisService(RedisConnection.Connection);
        var matchManager = new MatchManager(redisService);

        matchManager.Start();

        var random = new Random();
        var users = new List<MatchQueueItem>();

        var tasks = Enumerable.Range(1, 5000).Select(i =>
        {
            var mmr = random.Next(1, 100);
            users.Add(new MatchQueueItem(i).SetMMR(mmr));
            return Task.CompletedTask;
        });
        await Task.WhenAll(tasks);

        foreach (var u in users)
        {
            await matchManager.AddMatchQueueAsync(mode, u);
        }

        Console.ReadKey();
    }
}

