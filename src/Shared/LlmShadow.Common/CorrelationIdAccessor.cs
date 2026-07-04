namespace LlmShadow.Common;

/// <summary>Provides read/write access to the ambient correlation ID for the current operation.</summary>
public interface ICorrelationIdAccessor
{
    /// <summary>Gets the current correlation ID.</summary>
    string CorrelationId { get; }

    /// <summary>Replaces the current correlation ID with <paramref name="correlationId"/>.</summary>
    void SetCorrelationId(string correlationId);
}

/// <summary>Scoped implementation of <see cref="ICorrelationIdAccessor"/> that generates a new ID on construction.</summary>
public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private string _correlationId = Guid.NewGuid().ToString("N");

    /// <inheritdoc />
    public string CorrelationId => _correlationId;

    /// <inheritdoc />
    public void SetCorrelationId(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("Correlation ID must not be empty.", nameof(correlationId));

        _correlationId = correlationId;
    }
}
