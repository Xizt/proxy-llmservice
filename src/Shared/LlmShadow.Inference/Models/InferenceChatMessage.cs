namespace LlmShadow.Inference.Models;

/// <summary>A single role-attributed message passed to the DO Inference API.</summary>
public sealed record InferenceChatMessage
{
    /// <summary>Gets the message author role (<c>system</c>, <c>user</c>, or <c>assistant</c>).</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Gets the text content of the message.</summary>
    public string Content { get; init; } = string.Empty;
}
