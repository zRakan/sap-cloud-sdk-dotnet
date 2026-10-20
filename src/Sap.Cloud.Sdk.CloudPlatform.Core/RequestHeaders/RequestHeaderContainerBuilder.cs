namespace Sap.Cloud.Sdk.CloudPlatform.RequestHeaders;

/// <summary>Builds an <see cref="IRequestHeaderContainer"/>. Header names are matched case-insensitively.</summary>
public sealed class RequestHeaderContainerBuilder
{
    private readonly OrderedDictionary<string, List<string>> _headers = new(StringComparer.Ordinal);

    internal RequestHeaderContainerBuilder()
    {
    }

    /// <summary>Adds all headers of <paramref name="headerContainer"/>, keeping existing values.</summary>
    /// <param name="headerContainer">The headers to add.</param>
    /// <returns>This builder.</returns>
    public RequestHeaderContainerBuilder WithHeaders(IRequestHeaderContainer headerContainer)
    {
        ArgumentNullException.ThrowIfNull(headerContainer);
        foreach (var name in headerContainer.HeaderNames)
        {
            WithHeader(name, headerContainer.GetHeaderValues(name));
        }
        return this;
    }

    /// <summary>Adds values to a header, keeping existing values.</summary>
    /// <param name="name">The header name.</param>
    /// <param name="values">The values.</param>
    /// <returns>This builder.</returns>
    public RequestHeaderContainerBuilder WithHeader(string name, params IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(values);
        var key = DefaultRequestHeaderContainer.Normalize(name);
        if (!_headers.TryGetValue(key, out var existing))
        {
            _headers[key] = existing = [];
        }
        existing.AddRange(values);
        return this;
    }

    /// <summary>Adds the values that <paramref name="headerContainer"/> holds for header <paramref name="name"/>.</summary>
    /// <param name="name">The header name.</param>
    /// <param name="headerContainer">The source of the values.</param>
    /// <returns>This builder.</returns>
    public RequestHeaderContainerBuilder WithHeader(string name, IRequestHeaderContainer headerContainer)
    {
        ArgumentNullException.ThrowIfNull(headerContainer);
        return WithHeader(name, headerContainer.GetHeaderValues(name));
    }

    /// <summary>Removes a header.</summary>
    /// <param name="name">The header name.</param>
    /// <returns>This builder.</returns>
    public RequestHeaderContainerBuilder WithoutHeader(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        _headers.Remove(DefaultRequestHeaderContainer.Normalize(name));
        return this;
    }

    /// <summary>Replaces all values of a header.</summary>
    /// <param name="name">The header name.</param>
    /// <param name="values">The new values.</param>
    /// <returns>This builder.</returns>
    public RequestHeaderContainerBuilder ReplaceHeader(string name, params IEnumerable<string> values) =>
        WithoutHeader(name).WithHeader(name, values);

    /// <summary>Replaces a header with the values <paramref name="headerContainer"/> holds for it.</summary>
    /// <param name="name">The header name.</param>
    /// <param name="headerContainer">The source of the values.</param>
    /// <returns>This builder.</returns>
    public RequestHeaderContainerBuilder ReplaceHeader(string name, IRequestHeaderContainer headerContainer) =>
        WithoutHeader(name).WithHeader(name, headerContainer);

    /// <summary>Removes all headers.</summary>
    /// <returns>This builder.</returns>
    public RequestHeaderContainerBuilder Clear()
    {
        _headers.Clear();
        return this;
    }

    /// <summary>Creates the container.</summary>
    /// <returns>The container.</returns>
    public IRequestHeaderContainer Build() =>
        DefaultRequestHeaderContainer.FromMultiValueMap(
            _headers.Select(h => KeyValuePair.Create(h.Key, (IEnumerable<string?>?)h.Value)));
}
