using MatchMaking.Common;
using MatchMaking.Model;
using StackExchange.Redis;

namespace MatchMaking.Redis;

public delegate void OnMatchRequest(MatchMode mode);
public delegate void OnMatchComplete(MatchMode mode, int matchedUserCount);

public class RedisMessage
{
    public event OnMatchRequest? IncreaseMatchQueueEvent;
    public event OnMatchComplete? DecreaseMatchQueueEvent;

    private readonly Dictionary<string, RedisChannel> _channels;

    private ISubscriber _pubsub = RedisConnection.GetSubscriber();

    public RedisMessage()
    {
        _channels = new Dictionary<string, RedisChannel>()
        {
            { RedisKeys.MatchRequest, new RedisChannel(RedisKeys.MatchRequest, RedisChannel.PatternMode.Literal) },
            { RedisKeys.MatchComplete, new RedisChannel(RedisKeys.MatchComplete, RedisChannel.PatternMode.Literal) }
        };

        InitSubMatchQueue();
    }

    private void InitSubMatchQueue()
    {
        _pubsub.SubscribeAsync(GetChannel(RedisKeys.MatchRequest), (channel, value) =>
        {
            if (IncreaseMatchQueueEvent == null && !value.HasValue)
            {
                return;
            }

            IncreaseMatchQueueEvent?.Invoke(Converter.ToMatchMode(value.ToString()));
        });

        _pubsub.SubscribeAsync(GetChannel(RedisKeys.MatchComplete), (channel, value) =>
        {
            if (DecreaseMatchQueueEvent == null && !value.HasValue)
            {
                return;
            }

            var data = value.ToString().Split(':');
            var mode = Converter.ToMatchMode(data[0]);
            var count = int.Parse(data[1]);

            DecreaseMatchQueueEvent?.Invoke(mode, count);
        });

    }

    public void PubIncreaseMatchQueue(MatchMode mode)
    {
        _pubsub.Publish(GetChannel(RedisKeys.MatchRequest), Converter.ToFastString(mode));
    }

    public void PubDecreaseMatchQueue(MatchMode mode, int count)
    {
        _pubsub.Publish(GetChannel(RedisKeys.MatchComplete), $"{Converter.ToFastString(mode)}:{count}");
    }

    private RedisChannel GetChannel(string key)
    {
        if (!_channels.ContainsKey(key))
        {
            throw new ArgumentException($"Invalid key: {key}");
        }

        return _channels[key];
    }

}
