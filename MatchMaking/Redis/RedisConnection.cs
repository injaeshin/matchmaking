using StackExchange.Redis;

namespace MatchMaking.Redis;

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

    public static ISubscriber GetSubscriber() => Connection.GetSubscriber();

    public static IDatabase GetDatabase(int dbIndex = 0) => Connection.GetDatabase(dbIndex);

    public static void Dispose()
    {
        if (lazyConnection.IsValueCreated)
        {
            lazyConnection.Value.Dispose();
        }
    }
}
