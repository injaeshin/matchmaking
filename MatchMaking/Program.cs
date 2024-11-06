using MatchMaking.Common;
using MatchMaking.Data;
using MatchMaking.Match;
using MatchMaking.Model;

namespace MatchMaking
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            var redisService = new RedisService();

            var matchService = new MatchService(redisService);
            //var matchService = new MatchServiceSingle(redisService);

            var random = new Random();
            var users = new List<MatchQueueItem>();

            var tasks = Enumerable.Range(0, 5000).Select(i =>
            {
                var mmr = random.Next(1, 100);
                var user = new MatchQueueItem(i, mmr, 0);
                user.SetScore(Score.EncodeScore(user.MMR));
                users.Add(user);
                return Task.CompletedTask;
            });
            await Task.WhenAll(tasks);

            await redisService._AddQueueAndScore(users);

            //foreach (var u in users)
            //{
            //    await redisService.AddMatchQueue(u);
            //    await redisService.AddMatchScore(u);
            //}

            //Console.ReadKey();

            matchService.Start();

            Console.ReadKey();

            matchService.Stop();

            Console.ReadKey();
        }
    }
}

