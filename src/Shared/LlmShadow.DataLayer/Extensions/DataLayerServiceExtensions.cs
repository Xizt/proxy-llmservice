using LlmShadow.Common.Options;
using LlmShadow.DataLayer.Common;
using LlmShadow.DataLayer.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LlmShadow.DataLayer.Extensions;

/// <summary>Extension methods for registering data layer services.</summary>
public static class DataLayerServiceExtensions
{
    /// <summary>
    /// Registers <see cref="ShadowDbContext"/> (Npgsql/PostgreSQL), all repositories,
    /// and the <see cref="DatabaseMigrator"/> helper.
    /// Call <see cref="Common.Options.DatabaseOptions"/> binding before calling this method.
    /// </summary>
    /// <param name="services">The DI service collection.</param>
    /// <returns>The updated <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddDataLayer(this IServiceCollection services)
    {
        services.AddDbContext<ShadowDbContext>((sp, options) =>
        {
            var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            options.UseNpgsql(
                dbOptions.ConnectionString,
                npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 3));
        });

        services.AddScoped<IRequestRepository, RequestRepository>();
        services.AddScoped<ISecondaryResponseRepository, SecondaryResponseRepository>();
        services.AddScoped<DatabaseMigrator>();

        return services;
    }
}
