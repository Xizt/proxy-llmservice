namespace LlmShadow.DataLayer.Models;

/// <summary>Base class for all database entities. Provides a surrogate primary key plus standard audit and soft-delete fields.</summary>
public abstract class BaseEntity
{
    /// <summary>Gets or sets the surrogate primary key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the record was first inserted.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the most recent update to the record.</summary>
    public DateTime ModifiedAtUtc { get; set; }

    /// <summary>Gets or sets a value indicating whether this record is logically deleted. Filtered out by default via a global EF Core query filter.</summary>
    public bool IsDeleted { get; set; }
}
