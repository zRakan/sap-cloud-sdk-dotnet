using Sap.Cloud.Sdk.CloudPlatform.Threading.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Threading;

/// <summary>
/// A value stored in an <see cref="IThreadContext"/>, together with the information whether it is confidential.
/// </summary>
/// <remarks>
/// A property holds either a value or the exception that occurred while producing it. Confidential values (for
/// example tokens) are never included in <see cref="ToString"/>.
/// </remarks>
public class ThreadContextProperty
{
    private readonly Result<object> _value;

    /// <summary>Initializes a new property.</summary>
    /// <param name="value">The value or the failure that occurred while producing it.</param>
    /// <param name="isConfidential">Whether the value must be hidden from logs.</param>
    protected ThreadContextProperty(Result<object?> value, bool isConfidential)
    {
        _value = value.Bind(v => v is null
            ? Result.Failure<object>(new ThreadContextPropertyException("ThreadContextProperty value cannot be null."))
            : Result.Success(v));
        IsConfidential = isConfidential;
    }

    /// <summary>Initializes a property whose value is provided by an override of <see cref="Value"/>.</summary>
    /// <param name="isConfidential">Whether the value must be hidden from logs.</param>
    private protected ThreadContextProperty(bool isConfidential)
    {
        IsConfidential = isConfidential;
    }

    /// <summary>The value, or the failure that occurred while producing it.</summary>
    public virtual Result<object> Value => _value;

    /// <summary>Whether the value must be hidden from logs.</summary>
    public bool IsConfidential { get; }

    /// <summary>Creates a property holding <paramref name="value"/>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The property.</returns>
    public static ThreadContextProperty Of(object value) => new(Result.Success<object?>(value), false);

    /// <summary>Creates a confidential property holding <paramref name="value"/>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The property.</returns>
    public static ThreadContextProperty OfConfidential(object value) => new(Result.Success<object?>(value), true);

    /// <summary>Creates a property from a result. A failed result is stored as failure.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The result.</param>
    /// <returns>The property.</returns>
    public static ThreadContextProperty OfResult<T>(Result<T> value) => new(value.Map(v => (object?)v), false);

    /// <summary>Creates a confidential property from a result.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The result.</param>
    /// <returns>The property.</returns>
    public static ThreadContextProperty OfConfidentialResult<T>(Result<T> value) => new(value.Map(v => (object?)v), true);

    /// <summary>
    /// Runs <paramref name="valueFactory"/> immediately and stores its value, or the exception it throws.
    /// </summary>
    /// <remarks>Replaces <c>ThreadContextProperty.decorateCallable</c> from the Java SDK.</remarks>
    /// <param name="valueFactory">Produces the value.</param>
    /// <returns>The property.</returns>
    public static ThreadContextProperty Capture(Func<object> valueFactory) => new(Result.Of<object?>(valueFactory), false);

    /// <summary>Like <see cref="Capture"/>, but the value is confidential.</summary>
    /// <param name="valueFactory">Produces the value.</param>
    /// <returns>The property.</returns>
    public static ThreadContextProperty CaptureConfidential(Func<object> valueFactory) =>
        new(Result.Of<object?>(valueFactory), true);

    /// <summary>
    /// Creates a property whose value is computed on first access and then cached.
    /// </summary>
    /// <param name="valueFactory">Produces the value.</param>
    /// <returns>The property.</returns>
    public static ThreadContextProperty Lazy(Func<object> valueFactory) => new LazyProperty(valueFactory, false);

    /// <summary>Like <see cref="Lazy"/>, but the value is confidential.</summary>
    /// <param name="valueFactory">Produces the value.</param>
    /// <returns>The property.</returns>
    public static ThreadContextProperty LazyConfidential(Func<object> valueFactory) => new LazyProperty(valueFactory, true);

    /// <summary>Creates a copy of this property for use in a duplicated context.</summary>
    /// <returns>The copy.</returns>
    internal virtual ThreadContextProperty Copy() => new(_value.Map(v => (object?)v), IsConfidential);

    /// <inheritdoc/>
    public override string ToString() => IsConfidential ? "Property(value=(hidden))" : $"Property(value={_value})";
}

/// <summary>A <see cref="ThreadContextProperty"/> whose value is computed once, on first access. Thread-safe.</summary>
internal sealed class LazyProperty : ThreadContextProperty
{
    private readonly Func<object> _valueFactory;
    private readonly Lazy<Result<object>> _lazyValue;

    internal LazyProperty(Func<object> valueFactory, bool isConfidential)
        : base(isConfidential)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);
        _valueFactory = valueFactory;
        _lazyValue = new Lazy<Result<object>>(
            () => Result.Of<object?>(valueFactory).Bind(v => v is null
                ? Result.Failure<object>(new ThreadContextPropertyException("ThreadContextProperty value cannot be null."))
                : Result.Success(v)),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public bool HasBeenEvaluated => _lazyValue.IsValueCreated;

    public override Result<object> Value => _lazyValue.Value;

    // A copy is re-evaluated independently, matching the Java SDK.
    internal override ThreadContextProperty Copy() => new LazyProperty(_valueFactory, IsConfidential);

    public override string ToString()
    {
        if (IsConfidential)
        {
            return "LazyProperty(value=(hidden))";
        }
        // Do not evaluate just to produce a string.
        return _lazyValue.IsValueCreated
            ? $"LazyProperty(value={_lazyValue.Value})"
            : "LazyProperty(value=(not yet evaluated))";
    }
}
