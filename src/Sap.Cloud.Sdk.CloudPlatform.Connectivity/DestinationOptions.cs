namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Contributes parameters to a <see cref="DestinationOptions.Builder"/>.</summary>
public interface IDestinationOptionsAugmenter
{
    /// <summary>Adds parameters to the builder.</summary>
    /// <param name="builder">The builder.</param>
    void AugmentBuilder(DestinationOptions.Builder builder);
}

/// <summary>Parameters that customize how a <see cref="IDestinationLoader"/> loads a destination.</summary>
/// <remarks>The meaning of the parameters is defined by the loaders, for example the destination service module.</remarks>
public sealed class DestinationOptions : IEquatable<DestinationOptions>
{
    private readonly IReadOnlyDictionary<string, object?> _parameters;

    private DestinationOptions(IReadOnlyDictionary<string, object?> parameters) => _parameters = parameters;

    /// <summary>The names of all parameters.</summary>
    public IReadOnlyCollection<string> OptionKeys => [.. _parameters.Keys];

    /// <summary>Creates an empty builder.</summary>
    /// <returns>The builder.</returns>
    public static Builder CreateBuilder() => new();

    /// <summary>Creates a builder initialized with the parameters of other options.</summary>
    /// <param name="baseOptions">The options to start from.</param>
    /// <returns>The builder.</returns>
    public static Builder CreateBuilder(DestinationOptions baseOptions)
    {
        ArgumentNullException.ThrowIfNull(baseOptions);
        return new Builder(baseOptions);
    }

    /// <summary>Gets a parameter.</summary>
    /// <param name="key">The name.</param>
    /// <returns>The value, or <see langword="null"/> if the parameter is not set.</returns>
    public object? Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _parameters.GetValueOrDefault(key);
    }

    /// <inheritdoc/>
    public bool Equals(DestinationOptions? other) =>
        other is not null
        && _parameters.Count == other._parameters.Count
        && _parameters.All(p => other._parameters.TryGetValue(p.Key, out var v) && Equals(p.Value, v));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DestinationOptions);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var (key, value) in _parameters)
        {
            // Order independent, as dictionaries compare equal regardless of order.
            hash ^= HashCode.Combine(key, value);
        }
        return hash;
    }

    /// <summary>Lists the parameter names; values may be confidential.</summary>
    /// <returns>A string representation.</returns>
    public override string ToString() => $"DestinationOptions(keys=[{string.Join(", ", _parameters.Keys)}])";

    /// <summary>Builds <see cref="DestinationOptions"/>.</summary>
    public sealed class Builder
    {
        private readonly Dictionary<string, object?> _parameters = [];

        internal Builder()
        {
        }

        internal Builder(DestinationOptions baseOptions)
        {
            foreach (var (key, value) in baseOptions._parameters)
            {
                _parameters[key] = value;
            }
        }

        /// <summary>Sets a parameter, replacing an existing value.</summary>
        /// <param name="key">The name.</param>
        /// <param name="value">The value.</param>
        /// <returns>This builder.</returns>
        public Builder Parameter(string key, object? value)
        {
            ArgumentNullException.ThrowIfNull(key);
            _parameters[key] = value;
            return this;
        }

        /// <summary>Sets a parameter unless it is already set.</summary>
        /// <param name="key">The name.</param>
        /// <param name="value">The value.</param>
        /// <returns>This builder.</returns>
        public Builder ParameterIfAbsent(string key, object? value)
        {
            ArgumentNullException.ThrowIfNull(key);
            _parameters.TryAdd(key, value);
            return this;
        }

        /// <summary>Lets an augmenter add parameters.</summary>
        /// <param name="augmenter">The augmenter.</param>
        /// <returns>This builder.</returns>
        public Builder AugmentBuilder(IDestinationOptionsAugmenter augmenter)
        {
            ArgumentNullException.ThrowIfNull(augmenter);
            augmenter.AugmentBuilder(this);
            return this;
        }

        /// <summary>Creates the options.</summary>
        /// <returns>The options.</returns>
        public DestinationOptions Build() => new(new Dictionary<string, object?>(_parameters));
    }
}
