using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace LlmShadow.DataLayer.Common;

/// <summary>
/// Design-time factory that allows EF Core CLI tools (<c>dotnet ef migrations add</c>) to create
/// a <see cref="ShadowDbContext"/> without a running host.
/// Set the <c>Database__ConnectionString</c> environment variable or ensure an
/// <c>appsettings.Development.json</c> exists with a <c>Database:ConnectionString</c> key
/// before running EF tooling.
/// </summary>
public sealed class ShadowDbContextFactory : IDesignTimeDbContextFactory<ShadowDbContext>
{
    /// <inheritdoc />
    public ShadowDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config["Database:ConnectionString"]
            ?? throw new InvalidOperationException(
                "Database:ConnectionString is not configured. Set it via appsettings.Development.json or the Database__ConnectionString environment variable.");

        var options = new DbContextOptionsBuilder<ShadowDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ShadowDbContext(options);
    }
}
