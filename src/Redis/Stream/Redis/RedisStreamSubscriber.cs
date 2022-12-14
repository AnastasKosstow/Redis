using StackExchange.Redis;
using Redis.Common.Extensions;
using Redis.Common.Serialization;
using System.Runtime.Serialization;

namespace Redis.Stream;

public sealed class RedisStreamSubscriber : IStreamSubscriber
{
    private readonly ISubscriber subscriber;
    private readonly ISerializer serializer;

    public RedisStreamSubscriber(IConnectionMultiplexer connectionMultiplexer, ISerializer serializer)
    {
        this.subscriber = connectionMultiplexer.GetSubscriber();
        this.serializer = serializer;
    }

    public Task SubscribeAsync<T>(string channel, Action<T> handler) 
        where T : class
    {
        if (string.IsNullOrEmpty(channel))
        {
            throw new ArgumentException("Value cannot be null or empty.", nameof(channel));
        }

        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var result = subscriber.SubscribeAsync(channel, (_, data) =>
        {
            var payload = serializer.Deserialize<T>(data);
            if (payload is null)
            {
                string message = $"In {nameof(SubscribeAsync)}\nCannot deserialize payload: {data}";
                throw new SerializationException(message);
            }

            handler(payload);
        });

        return result;
    }

    public void Subscribe<T>(string channel, Action<T> handler)
        where T : class
    {
        if (string.IsNullOrEmpty(channel))
        {
            throw new ArgumentException("Value cannot be null or empty.", nameof(channel));
        }

        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var subscription = subscriber
            .SubscribeAsync(channel)
            .WaitAndUnwrapException();

        subscription.OnMessage(channelMessage =>
        {
            var payload = serializer.Deserialize<T>(channelMessage.Message);
            if (payload is null)
            {
                string message = $"In {nameof(SubscribeAsync)}\nCannot deserialize payload: {channelMessage.Message}";
                throw new SerializationException(message);
            }

            handler(payload);
        });
    }
}
