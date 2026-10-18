using PharmacyApp.Application.Common;
using Xunit;

namespace PharmacyApp.Application.Tests;

public class ResultTests
{
    [Fact]
    public void Success_ReturnsSuccessResult()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_ReturnsFailureResultWithErrorMessage()
    {
        const string errorMessage = "Medicine out of stock";
        var result = Result.Failure(errorMessage);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(errorMessage, result.Error);
    }

    [Fact]
    public void GenericSuccess_ContainsValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void GenericFailure_ThrowsWhenAccessingValue()
    {
        var result = Result.Failure<string>("Invalid batch number");

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("Invalid batch number", result.Error);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }
}
