namespace LlmShadow.Common;

/// <summary>Generic result wrapper used by service-layer methods to communicate success or failure without throwing exceptions.</summary>
/// <typeparam name="T">The type of the wrapped value.</typeparam>
public sealed class ServiceResult<T>
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess { get; private init; }

    /// <summary>Gets the result value when <see cref="IsSuccess"/> is <c>true</c>; otherwise <c>null</c>.</summary>
    public T? Value { get; private init; }

    /// <summary>Gets the human-readable error description when <see cref="IsSuccess"/> is <c>false</c>.</summary>
    public string? Error { get; private init; }

    /// <summary>Gets an optional machine-readable error code.</summary>
    public string? ErrorCode { get; private init; }

    private ServiceResult() { }

    /// <summary>Creates a successful result wrapping <paramref name="value"/>.</summary>
    public static ServiceResult<T> Success(T value) =>
        new() { IsSuccess = true, Value = value };

    /// <summary>Creates a failed result with the given <paramref name="error"/> message and optional <paramref name="errorCode"/>.</summary>
    public static ServiceResult<T> Failure(string error, string? errorCode = null) =>
        new() { IsSuccess = false, Error = error, ErrorCode = errorCode };
}
