using FluentAssertions;
using LlmShadow.Common.Exceptions;
using Xunit;

namespace LlmShadow.UnitTests.Common;

/// <summary>Unit tests for domain exception types.</summary>
public sealed class ExceptionTests
{
    // ── NotFoundException ─────────────────────────────────────────────────────

    [Fact]
    public void NotFoundException_WithResourceNameAndId_FormatsMessageCorrectly()
    {
        var ex = new NotFoundException("User", 42);
        ex.Message.Should().Contain("User");
        ex.Message.Should().Contain("42");
    }

    [Fact]
    public void NotFoundException_WithResourceNameAndId_HasNotFoundErrorCode()
    {
        var ex = new NotFoundException("Order", Guid.Empty);
        ex.ErrorCode.Should().Be("NOT_FOUND");
    }

    [Fact]
    public void NotFoundException_WithCustomMessage_UsesProvidedMessage()
    {
        const string message = "The resource you requested does not exist.";
        var ex = new NotFoundException(message);
        ex.Message.Should().Be(message);
    }

    [Fact]
    public void NotFoundException_WithCustomMessage_HasNotFoundErrorCode()
    {
        var ex = new NotFoundException("custom message");
        ex.ErrorCode.Should().Be("NOT_FOUND");
    }

    [Fact]
    public void NotFoundException_IsAssignableFromException()
    {
        var ex = new NotFoundException("thing", 1);
        ex.Should().BeAssignableTo<Exception>();
        ex.Should().BeAssignableTo<DomainException>();
    }

    // ── ConflictException ─────────────────────────────────────────────────────

    [Fact]
    public void ConflictException_HasConflictErrorCode()
    {
        var ex = new ConflictException("duplicate resource");
        ex.ErrorCode.Should().Be("CONFLICT");
    }

    [Fact]
    public void ConflictException_MessageIsPreserved()
    {
        const string message = "Resource already exists.";
        var ex = new ConflictException(message);
        ex.Message.Should().Be(message);
    }

    [Fact]
    public void ConflictException_IsAssignableFromDomainException()
    {
        var ex = new ConflictException("conflict");
        ex.Should().BeAssignableTo<DomainException>();
    }

    // ── DomainException ───────────────────────────────────────────────────────

    [Fact]
    public void DomainException_ErrorCodeIsAccessible()
    {
        // Test via concrete subtype
        DomainException ex = new NotFoundException("x");
        ex.ErrorCode.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void DomainException_BothSubtypesAreExceptions()
    {
        // Ensure both subtypes can be caught as Exception
        Action throwNotFound  = () => throw new NotFoundException("item", 1);
        Action throwConflict  = () => throw new ConflictException("conflict");

        throwNotFound.Should().ThrowExactly<NotFoundException>();
        throwConflict.Should().ThrowExactly<ConflictException>();
    }
}
