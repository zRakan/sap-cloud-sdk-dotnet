using System.Diagnostics.CodeAnalysis;

namespace Sap.Cloud.Sdk.CloudPlatform;

/// <summary>
/// The outcome of an operation that either produced a value or failed with an exception.
/// </summary>
/// <remarks>
/// Replaces <c>io.vavr.control.Try</c> from the Java SDK. Use it where the cause of a failure must be preserved and
/// passed on (for example when several lookups are attempted in turn and their failures are aggregated). For a simple
/// "value or nothing" result, prefer nullable return types instead.
/// </remarks>
/// <typeparam name="T">The type of the value.</typeparam>
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly T? _value;
    private readonly Exception? _exception;

    private Result(T? value, Exception? exception)
    {
        _value = value;
        _exception = exception;
    }

    /// <summary>Whether the operation succeeded.</summary>
    [MemberNotNullWhen(false, nameof(Exception))]
    public bool IsSuccess => _exception is null;

    /// <summary>Whether the operation failed.</summary>
    [MemberNotNullWhen(true, nameof(Exception))]
    public bool IsFailure => _exception is not null;

    /// <summary>The cause of the failure, or <see langword="null"/> on success.</summary>
    public Exception? Exception => _exception;

    /// <summary>Creates a successful result.</summary>
    /// <param name="value">The value.</param>
    /// <returns>A successful result holding <paramref name="value"/>.</returns>
#pragma warning disable CA1000 // Static factories on the generic type keep call sites short (Result<T>.Success(x)).
    public static Result<T> Success(T value) => new(value, null);

    /// <summary>Creates a failed result.</summary>
    /// <param name="exception">The cause of the failure.</param>
    /// <returns>A failed result.</returns>
    public static Result<T> Failure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new(default, exception);
    }
#pragma warning restore CA1000

    /// <summary>Returns the value, or throws the captured exception.</summary>
    /// <returns>The value.</returns>
    public T GetValueOrThrow()
    {
        if (_exception is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(_exception);
        }
        return _value!;
    }

    /// <summary>Returns the value, or throws an exception created from the failure cause.</summary>
    /// <param name="exceptionFactory">Creates the exception to throw from the failure cause.</param>
    /// <returns>The value.</returns>
    public T GetValueOrThrow(Func<Exception, Exception> exceptionFactory)
    {
        ArgumentNullException.ThrowIfNull(exceptionFactory);
        if (_exception is not null)
        {
            throw exceptionFactory(_exception);
        }
        return _value!;
    }

    /// <summary>Returns the value, or <see langword="default"/> on failure.</summary>
    /// <returns>The value or <see langword="default"/>.</returns>
    public T? GetValueOrDefault() => _value;

    /// <summary>Gets the value if the operation succeeded.</summary>
    /// <param name="value">The value, or <see langword="default"/> on failure.</param>
    /// <returns><see langword="true"/> on success.</returns>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = _value;
        return IsSuccess;
    }

    /// <summary>Transforms the value of a successful result. Exceptions thrown by the mapper become failures.</summary>
    /// <typeparam name="TResult">The new value type.</typeparam>
    /// <param name="mapper">The transformation.</param>
    /// <returns>The transformed result.</returns>
    public Result<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        if (_exception is not null)
        {
            return Result<TResult>.Failure(_exception);
        }
        var value = _value!;
        return Result.Of(() => mapper(value));
    }

    /// <summary>Chains an operation that itself returns a result.</summary>
    /// <typeparam name="TResult">The new value type.</typeparam>
    /// <param name="binder">The operation.</param>
    /// <returns>The result of <paramref name="binder"/>, or this failure.</returns>
    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);
        if (_exception is not null)
        {
            return Result<TResult>.Failure(_exception);
        }
        try
        {
            return binder(_value!);
        }
#pragma warning disable CA1031 // Capturing every exception is the purpose of this type.
        catch (Exception e)
#pragma warning restore CA1031
        {
            return Result<TResult>.Failure(e);
        }
    }

    /// <summary>Returns this result if successful, otherwise the result of <paramref name="alternative"/>.</summary>
    /// <param name="alternative">Produces the alternative result.</param>
    /// <returns>This result or the alternative.</returns>
    public Result<T> OrElse(Func<Result<T>> alternative)
    {
        ArgumentNullException.ThrowIfNull(alternative);
        return IsSuccess ? this : alternative();
    }

    /// <summary>Turns a failure into a success using <paramref name="recovery"/>.</summary>
    /// <param name="recovery">Produces a value from the failure cause.</param>
    /// <returns>This result if successful, otherwise the recovered result.</returns>
    public Result<T> Recover(Func<Exception, T> recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        if (_exception is null)
        {
            return this;
        }
        var cause = _exception;
        return Result.Of(() => recovery(cause));
    }

    /// <summary>Runs <paramref name="action"/> with the failure cause if this result failed.</summary>
    /// <param name="action">The action.</param>
    /// <returns>This result.</returns>
    public Result<T> OnFailure(Action<Exception> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_exception is not null)
        {
            action(_exception);
        }
        return this;
    }

    /// <inheritdoc/>
    public bool Equals(Result<T> other) =>
        EqualityComparer<T?>.Default.Equals(_value, other._value) && Equals(_exception, other._exception);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_value, _exception);

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString() =>
        _exception is null ? $"Success({_value})" : $"Failure({_exception.GetType().Name}: {_exception.Message})";
}

/// <summary>Factory methods for <see cref="Result{T}"/>.</summary>
public static class Result
{
    /// <summary>Runs <paramref name="func"/> and captures its value or exception.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="func">The operation.</param>
    /// <returns>The captured result.</returns>
    public static Result<T> Of<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        try
        {
            return Result<T>.Success(func());
        }
#pragma warning disable CA1031 // Capturing every exception is the purpose of this type.
        catch (Exception e)
#pragma warning restore CA1031
        {
            return Result<T>.Failure(e);
        }
    }

    /// <summary>Creates a successful result.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>A successful result.</returns>
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    /// <summary>Creates a failed result.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="exception">The cause.</param>
    /// <returns>A failed result.</returns>
    public static Result<T> Failure<T>(Exception exception) => Result<T>.Failure(exception);
}
