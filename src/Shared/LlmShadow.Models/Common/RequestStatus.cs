namespace LlmShadow.Models.Common;

/// <summary>Represents the lifecycle status of a proxied request and its shadow evaluation.</summary>
public enum RequestStatus
{
    /// <summary>Request received, primary response stored; shadow processing pending or in-flight.</summary>
    Created = 0,

    /// <summary>Both responses evaluated and the <c>action</c> keys matched exactly.</summary>
    Matched = 1,

    /// <summary>Both responses evaluated but the <c>action</c> keys did not match, or one was unparseable.</summary>
    Failed = 2,

    /// <summary>The primary LLM call returned an error; shadow processing was still queued.</summary>
    PrimaryLLMResponseFailed = 3,

    /// <summary>The secondary (shadow) LLM call returned an error or produced an invalid response.</summary>
    SecondaryLLMResponseFailed = 4,

    /// <summary>The secondary LLM call exceeded its allowed timeout.</summary>
    Timedout = 5
}
