using System.Diagnostics.CodeAnalysis;

namespace RestrictPoint.Common;

/// <summary>
/// Discriminated result for expected failures. Exceptions are reserved for unexpected failures
/// per coding standards (docs/07): "Use Result&lt;T&gt; for expected failures."
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T value)
    {
        _value = value;
        _error = null;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        _value = default;
        _error = error;
        IsSuccess = false;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>The success value. Throws if accessed on a failed result.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access {nameof(Value)} of a failed result ({_error!.Code}).");

    /// <summary>The error. Null when the result is successful.</summary>
    public Error? Error => _error;

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);

    /// <summary>Named factory used by the non-generic <see cref="Result"/> helper.</summary>
    internal static Result<T> Success(T value) => new(value);

    /// <summary>Named factory used by the non-generic <see cref="Result"/> helper.</summary>
    internal static Result<T> Failure(Error error) => new(error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(_error!);
}

/// <summary>Factory helpers for <see cref="Result{T}"/> and the unit result.</summary>
public static class Result
{
    /// <summary>Marker type for operations that succeed without producing a value.</summary>
    public readonly record struct Unit
    {
        public static readonly Unit Value;
    }

    public static Result<Unit> Success() => Result<Unit>.Success(default);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<Unit> Failure(Error error) => Result<Unit>.Failure(error);

    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);
}
