namespace LlmShadow.Common.Exceptions;

/// <summary>Thrown when an operation cannot proceed due to a conflicting state, such as a duplicate resource.</summary>
public sealed class ConflictException : DomainException
{
    /// <summary>Initialises a conflict exception with a <paramref name="message"/>.</summary>
    public ConflictException(string message)
        : base(message, "CONFLICT") { }
}
