using System.Collections;
using System.Globalization;
using System.Text;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>An immutable, generic destination: a case-insensitive map of properties.</summary>
/// <remarks>
/// <see cref="ToString"/> hides the values of properties that may hold secrets. Java hides names containing
/// <c>password</c> or <c>secret</c>; this port additionally hides names containing <c>privatekey</c>,
/// <c>authtokens</c>, <c>certificates</c>, <c>authorization</c> or <c>token</c> (unless the name contains <c>url</c>).
/// </remarks>
public sealed class DefaultDestination : IDestination, IEquatable<DefaultDestination>
{
    private static readonly string[] SensitiveFragments =
        ["password", "secret", "privatekey", "authtokens", "certificates", "authorization", "token"];

    private readonly SortedDictionary<string, object> _properties = new(StringComparer.OrdinalIgnoreCase);

    private DefaultDestination(IReadOnlyDictionary<string, object> properties)
    {
        foreach (var (key, value) in properties)
        {
            _properties[key] = value;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> PropertyNames => [.. _properties.Keys];

    /// <summary>Creates a builder with the given properties.</summary>
    /// <param name="map">The properties.</param>
    /// <returns>The builder.</returns>
    public static Builder FromMap(IReadOnlyDictionary<string, object> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var builder = new Builder();
        foreach (var (key, value) in map)
        {
            builder.Property(key, value);
        }
        return builder;
    }

    /// <summary>Creates an empty builder.</summary>
    /// <returns>The builder.</returns>
    public static Builder CreateBuilder() => new();

    /// <inheritdoc/>
    public object? GetProperty(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _properties.GetValueOrDefault(key);
    }

    /// <summary>Creates a builder initialized with the properties of this destination.</summary>
    /// <returns>The builder.</returns>
    public Builder ToBuilder() => FromMap(_properties);

    /// <inheritdoc/>
    public bool Equals(DefaultDestination? other) =>
        other is not null && (ReferenceEquals(this, other) || DestinationPropertyComparer.PropertiesEqual(_properties, other._properties));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DefaultDestination);

    /// <inheritdoc/>
    public override int GetHashCode() => DestinationPropertyComparer.PropertiesHash(_properties);

    /// <summary>Lists the properties, hiding those that may hold secrets.</summary>
    /// <returns>A string representation.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder("DefaultDestination(properties={");
        var first = true;
        foreach (var (key, value) in _properties)
        {
            if (!first)
            {
                sb.Append(", ");
            }
            first = false;
            sb.Append(key).Append('=').Append(IsSensitive(key) ? "(hidden)" : Convert.ToString(value, CultureInfo.InvariantCulture));
        }
        return sb.Append("})").ToString();
    }

    private static bool IsSensitive(string key)
    {
        var lower = key.ToLowerInvariant();
        return !lower.Contains("url", StringComparison.Ordinal)
            && SensitiveFragments.Any(fragment => lower.Contains(fragment, StringComparison.Ordinal));
    }

    /// <summary>Builds <see cref="DefaultDestination"/>s.</summary>
    public sealed class Builder
    {
        internal Builder()
        {
        }

        internal SortedDictionary<string, object> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Sets a property.</summary>
        /// <param name="key">The property name (case-insensitive).</param>
        /// <param name="value">The value.</param>
        /// <returns>This builder.</returns>
        public Builder Property(string key, object value)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(value);
            Properties[key] = value;
            return this;
        }

        /// <summary>Sets a property.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="key">The typed key.</param>
        /// <param name="value">The value.</param>
        /// <returns>This builder.</returns>
        public Builder Property<T>(DestinationPropertyKey<T> key, T value)
            where T : notnull
        {
            ArgumentNullException.ThrowIfNull(key);
            return Property(key.KeyName, value);
        }

        /// <summary>Reads a property.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="key">The typed key.</param>
        /// <param name="value">The converted value.</param>
        /// <returns><see langword="true"/> if the property is set.</returns>
        public bool TryGet<T>(DestinationPropertyKey<T> key, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out T value)
        {
            ArgumentNullException.ThrowIfNull(key);
            return key.TryGet(Properties, out value);
        }

        /// <summary>Removes a property.</summary>
        /// <param name="key">The typed key.</param>
        /// <returns>This builder.</returns>
        public Builder RemoveProperty<T>(DestinationPropertyKey<T> key)
        {
            ArgumentNullException.ThrowIfNull(key);
            return RemoveProperty(key.KeyName);
        }

        /// <summary>Removes a property.</summary>
        /// <param name="key">The property name.</param>
        /// <returns>This builder.</returns>
        public Builder RemoveProperty(string key)
        {
            ArgumentNullException.ThrowIfNull(key);
            Properties.Remove(key);
            return this;
        }

        /// <summary>Sets the destination name.</summary>
        /// <param name="name">The name.</param>
        /// <returns>This builder.</returns>
        public Builder Name(string name) => Property(DestinationProperty.Name, name);

        /// <summary>Sets the destination URL.</summary>
        /// <param name="uri">The URL.</param>
        /// <returns>This builder.</returns>
        public Builder Uri(string uri) => Property(DestinationProperty.Uri, uri);

        /// <summary>Sets the destination URL.</summary>
        /// <param name="uri">The URL.</param>
        /// <returns>This builder.</returns>
        public Builder Uri(Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);
            return Uri(uri.OriginalString);
        }

        /// <summary>Creates the destination.</summary>
        /// <returns>The destination.</returns>
        public DefaultDestination Build() => new(Properties);

        /// <summary>Lists the property names without their values.</summary>
        /// <returns>A string representation.</returns>
        public override string ToString() => $"DefaultDestination.Builder(properties=[{string.Join(", ", Properties.Keys)}])";
    }
}

/// <summary>Structural comparison of property maps, whose values may be collections.</summary>
internal static class DestinationPropertyComparer
{
    public static bool PropertiesEqual(IReadOnlyDictionary<string, object> a, IReadOnlyDictionary<string, object> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        foreach (var (key, value) in a)
        {
            if (!b.TryGetValue(key, out var other) || !ValuesEqual(value, other))
            {
                return false;
            }
        }
        return true;
    }

    public static int PropertiesHash(IReadOnlyDictionary<string, object> properties)
    {
        var hash = new HashCode();
        foreach (var (key, value) in properties.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            hash.Add(key, StringComparer.OrdinalIgnoreCase);
            hash.Add(ValueHash(value));
        }
        return hash.ToHashCode();
    }

    private static bool ValuesEqual(object a, object b)
    {
        if (a is string || b is string)
        {
            return Equals(a, b);
        }
        if (a is IDictionary da && b is IDictionary db)
        {
            return da.Count == db.Count && da.Keys.Cast<object>().All(k => db.Contains(k) && ValuesEqual(da[k]!, db[k]!));
        }
        if (a is IEnumerable ea && b is IEnumerable eb)
        {
            var la = ea.Cast<object?>().ToList();
            var lb = eb.Cast<object?>().ToList();
            return la.Count == lb.Count && la.Zip(lb).All(p => p.First is null ? p.Second is null : p.Second is not null && ValuesEqual(p.First, p.Second));
        }
        return Equals(a, b);
    }

    private static int ValueHash(object value) => value switch
    {
        string s => s.GetHashCode(StringComparison.Ordinal),
        IDictionary d => d.Count,
        IEnumerable e => e.Cast<object?>().Aggregate(17, (h, v) => (h * 31) + (v is null ? 0 : ValueHash(v))),
        _ => value.GetHashCode(),
    };
}
