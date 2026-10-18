namespace PharmacyApp.Application.Common;

/// <summary>
/// A discriminated result type used by all command and query handlers.
/// Avoids exception-as-control-flow for expected business failures.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }

    protected Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);

    public static Result<T> Success<T>(T value) => Result<T>.Ok(value);
    public static Result<T> Failure<T>(string error) => Result<T>.Fail(error);
}

/// <summary>
/// A result carrying a value on success.
/// </summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value of a failed result.");

    private Result(bool isSuccess, string? error, T? value)
        : base(isSuccess, error)
    {
        _value = value;
    }

    internal static Result<T> Ok(T value) => new(true, null, value);
    internal static Result<T> Fail(string error) => new(false, error, default);
}
