namespace LlmShadow.Common.Exceptions;

/// <summary>Base class for all domain-specific exceptions in the shadow LLM system.</summary>
public abstract class DomainException : Exception
{
    /// <summary>Machine-readable error code surfaced in Problem Details responses.</summary>
    public string ErrorCode { get; }

    /// <summary>Initialises the exception with a <paramref name="message"/> and an optional <paramref name="errorCode"/>.</summary>
    protected DomainException(string message, string errorCode, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }
}
