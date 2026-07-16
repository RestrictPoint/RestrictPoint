using RestrictPoint.Common;
using Xunit;

namespace RestrictPoint.Api.Identity.Tests.Common;

public sealed class ResultTests
{
    private static readonly Error TestError = Error.NotFound("test.not_found", "Missing.");

    [Fact]
    public void Success_exposes_value_and_no_error()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_exposes_error_and_guards_value()
    {
        Result<int> result = TestError;

        Assert.True(result.IsFailure);
        Assert.Equal(TestError, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Match_dispatches_by_state()
    {
        Result<int> success = 7;
        Result<int> failure = TestError;

        Assert.Equal("7", success.Match(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture), e => e.Code));
        Assert.Equal("test.not_found", failure.Match(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture), e => e.Code));
    }

    [Fact]
    public void Error_factories_set_kind()
    {
        Assert.Equal(ErrorKind.Validation, Error.Validation("c", "m").Kind);
        Assert.Equal(ErrorKind.Unauthorized, Error.Unauthorized("c", "m").Kind);
        Assert.Equal(ErrorKind.Forbidden, Error.Forbidden("c", "m").Kind);
        Assert.Equal(ErrorKind.NotFound, Error.NotFound("c", "m").Kind);
        Assert.Equal(ErrorKind.Conflict, Error.Conflict("c", "m").Kind);
        Assert.Equal(ErrorKind.Unexpected, Error.Unexpected("c", "m").Kind);
    }
}
