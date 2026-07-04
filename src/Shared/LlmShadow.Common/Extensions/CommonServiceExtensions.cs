using LlmShadow.Common.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LlmShadow.Common.Extensions;

/// <summary>Extension methods for registering common cross-cutting services.</summary>
public static class CommonServiceExtensions
{
    /// <summary>
    /// Registers <see cref="ICorrelationIdAccessor"/> as a scoped service and binds all shared
    /// options sections from <paramref name="configuration"/>.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The updated <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddCommonServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ICorrelationIdAccessor, CorrelationIdAccessor>();

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<InferenceOptions>(configuration.GetSection(InferenceOptions.SectionName));
        services.Configure<EvaluatorOptions>(configuration.GetSection(EvaluatorOptions.SectionName));
        services.Configure<ProcessorOptions>(configuration.GetSection(ProcessorOptions.SectionName));

        return services;
    }
}
