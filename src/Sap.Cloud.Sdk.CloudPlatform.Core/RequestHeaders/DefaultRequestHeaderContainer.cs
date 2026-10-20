namespace Sap.Cloud.Sdk.CloudPlatform.RequestHeaders;

/// <summary>Default implementation of <see cref="IRequestHeaderContainer"/>.</summary>
public sealed class DefaultRequestHeaderContainer : IRequestHeaderContainer, IEquatable<DefaultRequestHeaderContainer>
{
    private readonly OrderedDictionary<string, string[]> _headers;

    private DefaultRequestHeaderContainer(OrderedDictionary<string, string[]> headers)
    {
        _headers = headers;
    }

    /// <summary>A container without headers.</summary>
    public static IRequestHeaderContainer Empty { get; } = new DefaultRequestHeaderContainer(new());

    /// <summary>Creates a container from single-valued headers. Entries with a <see langword="null"/> value are skipped.</summary>
    /// <param name="headers">The headers.</param>
    /// <returns>The container.</returns>
    public static IRequestHeaderContainer FromSingleValueMap(IEnumerable<KeyValuePair<string, string?>> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        return FromMultiValueMap(headers.Select(h => KeyValuePair.Create(h.Key, (IEnumerable<string?>?)[h.Value])));
    }

    /// <summary>
    /// Creates a container from multi-valued headers. Names are normalized; <see langword="null"/> names and values are
    /// skipped, and values of headers whose names differ only in case are merged.
    /// </summary>
    /// <param name="headers">The headers.</param>
    /// <returns>The container.</returns>
    public static IRequestHeaderContainer FromMultiValueMap(
        IEnumerable<KeyValuePair<string, IEnumerable<string?>?>> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        var normalized = new OrderedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (name, values) in headers)
        {
            if (name is null || values is null)
            {
                continue;
            }
            var nonNullValues = values.OfType<string>().ToList();
            if (nonNullValues.Count == 0)
            {
                continue;
            }
            var key = Normalize(name);
            if (!normalized.TryGetValue(key, out var existing))
            {
                normalized[key] = existing = [];
            }
            existing.AddRange(nonNullValues);
        }

        var result = new OrderedDictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (key, values) in normalized)
        {
            result[key] = [.. values];
        }
        return new DefaultRequestHeaderContainer(result);
    }

    /// <summary>Creates an empty builder.</summary>
    /// <returns>The builder.</returns>
    public static RequestHeaderContainerBuilder Builder() => new();

    internal static string Normalize(string headerName) => headerName.Trim().ToLowerInvariant();

    /// <inheritdoc/>
    public IReadOnlyList<string> HeaderNames => [.. _headers.Keys];

    /// <inheritdoc/>
    public bool ContainsHeader(string headerName)
    {
        ArgumentNullException.ThrowIfNull(headerName);
        return _headers.ContainsKey(Normalize(headerName));
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetHeaderValues(string headerName)
    {
        ArgumentNullException.ThrowIfNull(headerName);
        return _headers.TryGetValue(Normalize(headerName), out var values) ? values.AsReadOnly() : [];
    }

    /// <inheritdoc/>
    public RequestHeaderContainerBuilder ToBuilder()
    {
        var builder = new RequestHeaderContainerBuilder();
        foreach (var (name, values) in _headers)
        {
            builder.WithHeader(name, values);
        }
        return builder;
    }

    /// <inheritdoc/>
    public bool Equals(DefaultRequestHeaderContainer? other) =>
        other is not null
        && _headers.Count == other._headers.Count
        && _headers.All(h => other._headers.TryGetValue(h.Key, out var v) && h.Value.SequenceEqual(v));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DefaultRequestHeaderContainer);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        // Order-independent over header names, consistent with Equals.
        var hash = 0;
        foreach (var (name, values) in _headers)
        {
            var entry = new HashCode();
            entry.Add(name);
            foreach (var value in values)
            {
                entry.Add(value);
            }
            hash ^= entry.ToHashCode();
        }
        return hash;
    }

    /// <summary>Returns the header names only; values may be confidential.</summary>
    /// <returns>A string representation.</returns>
    public override string ToString() => $"DefaultRequestHeaderContainer(headerNames=[{string.Join(", ", _headers.Keys)}])";
}
