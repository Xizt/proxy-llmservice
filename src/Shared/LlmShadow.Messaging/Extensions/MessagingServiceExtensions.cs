using LlmShadow.Common.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LlmShadow.Messaging.Extensions;

/// <summary>Extension methods for registering Redis messaging services.</summary>
public static class MessagingServiceExtensions
{
    /// <summary>
    /// Registers the <see cref="IConnectionMultiplexer"/> singleton and both
    /// <see cref="IShadowQueuePublisher"/> and <see cref="IShadowQueueConsumer"/> implementations.
    /// Requires <see cref="RedisOptions"/> to be bound before calling this method.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <returns>The updated <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
            return ConnectionMultiplexer.Connect(opts.ConnectionString);
        });

        services.AddSingleton<IShadowQueuePublisher, RedisStreamPublisher>();
        services.AddSingleton<IShadowQueueConsumer, RedisStreamConsumer>();

        return services;
    }
}
