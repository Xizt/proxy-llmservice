namespace LlmShadow.Common.Exceptions;

/// <summary>Thrown when a requested resource cannot be found.</summary>
public sealed class NotFoundException : DomainException
{
    /// <summary>Initialises a not-found exception for <paramref name="resourceName"/> with identifier <paramref name="id"/>.</summary>
    public NotFoundException(string resourceName, object id)
        : base($"{resourceName} with id '{id}' was not found.", "NOT_FOUND") { }

    /// <summary>Initialises a not-found exception with a custom <paramref name="message"/>.</summary>
    public NotFoundException(string message)
        : base(message, "NOT_FOUND") { }
}
