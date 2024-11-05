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

            var random = new Random();
            var users = new List<MatchQueueItem>();
            for (int i = 1; i <= 5000; i++)
            {
                var mmr = random.Next(1, 100);
                users.Add(new MatchQueueItem(i, mmr, 0));
            }

            for (int i = 0; i < 5000; i++)
            {
                var user = users[i];
                await matchService.AddMatchQueue(user);
            }

            matchService.Start();

            Console.ReadKey();

            matchService.Stop();

            Console.ReadKey();
        }
    }
}

