using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LlmShadow.DataLayer.Common;

/// <summary>Applies any pending EF Core migrations at application startup.</summary>
public sealed class DatabaseMigrator
{
    private readonly ShadowDbContext _db;
    private readonly ILogger<DatabaseMigrator> _logger;

    /// <summary>Initialises the migrator with the application's <see cref="ShadowDbContext"/>.</summary>
    public DatabaseMigrator(ShadowDbContext db, ILogger<DatabaseMigrator> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Applies all pending migrations synchronously-safe via an async overload.
    /// Call this from the startup pipeline before the application begins serving traffic.
    /// </summary>
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking for pending database migrations...");
        var pending = await _db.Database.GetPendingMigrationsAsync(cancellationToken);
        var pendingList = pending.ToList();

        if (pendingList.Count == 0)
        {
            _logger.LogInformation("Database schema is up to date");
            return;
        }

        _logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
            pendingList.Count, string.Join(", ", pendingList));

        await _db.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("Database migrations applied successfully");
    }
}
