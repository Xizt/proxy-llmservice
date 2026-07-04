using LlmShadow.DataLayer.Models;
using Microsoft.Extensions.Logging;

namespace LlmShadow.DataLayer.Repositories;

/// <summary>EF Core implementation of <see cref="ISecondaryResponseRepository"/>.</summary>
public sealed class SecondaryResponseRepository : ISecondaryResponseRepository
{
    private readonly ShadowDbContext _db;
    private readonly ILogger<SecondaryResponseRepository> _logger;

    /// <summary>Initialises the repository with a scoped <see cref="ShadowDbContext"/>.</summary>
    public SecondaryResponseRepository(ShadowDbContext db, ILogger<SecondaryResponseRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AddAsync(SecondaryLlmResponse response, CancellationToken cancellationToken = default)
    {
        try
        {
            _db.SecondaryLlmResponses.Add(response);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist secondary response for request {RequestId}", response.RequestId);
            throw;
        }
    }
}
