namespace LlmShadow.Common.Options;

/// <summary>Configuration options for the PostgreSQL database connection.</summary>
public sealed class DatabaseOptions
{
    /// <summary>Configuration section key.</summary>
    public const string SectionName = "Database";

    /// <summary>Gets the connection string for the DO Managed PostgreSQL instance.</summary>
    public string ConnectionString { get; init; } = string.Empty;
}
