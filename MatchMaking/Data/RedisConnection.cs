using StackExchange.Redis;

namespace MatchMaking.Data
{
    public class RedisConnection
    {
        private static Lazy<ConnectionMultiplexer> lazyConnection;

        static RedisConnection()
        {
            var configurationOptions = new ConfigurationOptions
            {
                EndPoints = { "127.0.0.1:6379" },
                AbortOnConnectFail = false,
                SyncTimeout = 5000,
                AsyncTimeout = 5000,
            };

            lazyConnection = new Lazy<ConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(configurationOptions));
        }

        public static ConnectionMultiplexer Connection => lazyConnection.Value;

        public static IDatabase GetDatabase(int dbIndex = 0)
        {
            return Connection.GetDatabase(dbIndex);
        }
    }
}
