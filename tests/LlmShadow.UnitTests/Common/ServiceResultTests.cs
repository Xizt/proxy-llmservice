using FluentAssertions;
using LlmShadow.Common;
using Xunit;

namespace LlmShadow.UnitTests.Common;

/// <summary>Unit tests for <see cref="ServiceResult{T}"/>.</summary>
public sealed class ServiceResultTests
{
    [Fact]
    public void Success_IsSuccessIsTrue()
    {
        var result = ServiceResult<string>.Success("hello");
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Success_ValueContainsProvidedData()
    {
        var result = ServiceResult<int>.Success(42);
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Success_ErrorAndErrorCodeAreNull()
    {
        var result = ServiceResult<string>.Success("hello");
        result.Error.Should().BeNull();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Failure_IsSuccessIsFalse()
    {
        var result = ServiceResult<string>.Failure("something went wrong");
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Failure_ErrorContainsMessage()
    {
        var result = ServiceResult<string>.Failure("not found");
        result.Error.Should().Be("not found");
    }

    [Fact]
    public void Failure_ValueIsNull()
    {
        var result = ServiceResult<string>.Failure("oops");
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Failure_WithErrorCode_ErrorCodeIsSet()
    {
        var result = ServiceResult<string>.Failure("conflict", "CONFLICT");
        result.ErrorCode.Should().Be("CONFLICT");
        result.Error.Should().Be("conflict");
    }

    [Fact]
    public void Failure_WithoutErrorCode_ErrorCodeIsNull()
    {
        var result = ServiceResult<string>.Failure("oops");
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Success_WorksWithNullableReferenceType()
    {
        var result = ServiceResult<string?>.Success(null);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Success_WorksWithValueType()
    {
        var result = ServiceResult<bool>.Success(false);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }
}
