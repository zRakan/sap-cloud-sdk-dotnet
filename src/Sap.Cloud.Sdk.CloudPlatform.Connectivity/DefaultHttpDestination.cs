using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.RequestHeaders;
using Sap.Cloud.Sdk.CloudPlatform.Security;
using BasicCreds = Sap.Cloud.Sdk.CloudPlatform.Security.BasicCredentials;
using AuthType = Sap.Cloud.Sdk.CloudPlatform.Connectivity.AuthenticationType;
using ProxyKind = Sap.Cloud.Sdk.CloudPlatform.Connectivity.ProxyType;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// The default implementation of <see cref="IHttpDestination"/>: destination properties plus custom headers, header
/// providers, a key store and a trust store.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GetHeaders(Uri)"/> returns, in this order: the custom headers, the headers of the custom and registered
/// <see cref="IDestinationHeaderProvider"/>s, the <c>URL.headers.*</c> properties, an <c>Authorization</c> header derived
/// from the authentication type (Basic credentials, or the forwarded <c>Authorization</c> header of the current request
/// for token forwarding) unless one is already present, and a <c>Proxy-Authorization</c> header unless one is present.
/// OAuth based authentication types are handled by header providers contributed by other modules.
/// </para>
/// <para>
/// <see cref="ToString"/> prints only the name, URI (without user info), proxy type and authentication type.
/// Differences from Java: key and trust stores are
/// <see cref="X509Certificate2Collection"/>s; builder methods that Java declares package-private but that other
/// modules need are public.
/// </para>
/// </remarks>
public sealed class DefaultHttpDestination : IHttpDestination, IEquatable<DefaultHttpDestination>
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<DefaultHttpDestination>();

    private readonly DefaultDestination _baseProperties;
    private readonly Func<X509Certificate2Collection?> _keyStoreSupplier;
    private readonly X509Certificate2Collection? _trustStore;
    private readonly IReadOnlyList<IDestinationHeaderProvider> _customHeaderProviders;
    private readonly IReadOnlyList<IDestinationHeaderProvider> _registeredHeaderProviders;
    private readonly Uri _uri;

    // The following values are derived from _baseProperties only and are computed once for performance.
    private readonly ProxyConfiguration? _proxyConfiguration;
    private readonly ProxyKind? _proxyType;
    private readonly BasicCredentials? _basicCredentials;
    private readonly AuthType _authenticationType;
    private readonly IReadOnlyList<Header> _headersFromProperties;
    private readonly IReadOnlyList<Header> _proxyAuthorizationHeaders;

    private DefaultHttpDestination(
        DefaultDestination baseProperties,
        IEnumerable<Header> customHeaders,
        Func<X509Certificate2Collection?> keyStoreSupplier,
        X509Certificate2Collection? trustStore,
        IEnumerable<IDestinationHeaderProvider> customHeaderProviders)
    {
        if (!CanBeConstructedFrom(baseProperties))
        {
            throw new ArgumentException(
                $"{baseProperties.GetType().Name} is misconfigured. URL doesn't exist or is not a valid URI.",
                nameof(baseProperties));
        }
        _baseProperties = baseProperties;
        _uri = ParseUri(baseProperties);
        CustomHeaders = [.. customHeaders];
        _customHeaderProviders = [.. customHeaderProviders];
        _registeredHeaderProviders = DestinationHeaderProviders.Registered;
        _keyStoreSupplier = keyStoreSupplier;
        _trustStore = trustStore;

        _proxyConfiguration = ComplexDestinationPropertyFactory.GetProxyConfiguration(baseProperties);
        _proxyType = ComplexDestinationPropertyFactory.GetProxyType(baseProperties);
        _basicCredentials = ComplexDestinationPropertyFactory.GetBasicCredentials(baseProperties);
        _authenticationType = ComplexDestinationPropertyFactory.GetAuthenticationType(baseProperties, _basicCredentials);
        _headersFromProperties = ComplexDestinationPropertyFactory.GetHeadersFromProperties(baseProperties);
        _proxyAuthorizationHeaders = ComplexDestinationPropertyFactory.GetProxyAuthorizationHeaders(_proxyConfiguration);
    }

    /// <summary>The headers that were added explicitly via the builder.</summary>
    public IReadOnlyList<Header> CustomHeaders { get; }

    /// <summary>The header providers that were added explicitly via the builder.</summary>
    public IReadOnlyList<IDestinationHeaderProvider> CustomHeaderProviders => _customHeaderProviders;

    internal Func<X509Certificate2Collection?> KeyStoreSupplier => _keyStoreSupplier;

    /// <inheritdoc/>
    public IReadOnlyCollection<string> PropertyNames => _baseProperties.PropertyNames;

    /// <inheritdoc/>
    public Uri Uri => _uri;

    /// <inheritdoc/>
    public string? TlsVersion => _baseProperties.TryGet(DestinationProperty.TlsVersion, out var v) ? v : null;

    /// <inheritdoc/>
    public ProxyConfiguration? ProxyConfiguration => _proxyConfiguration;

    /// <inheritdoc/>
    public X509Certificate2Collection? KeyStore => _keyStoreSupplier();

    /// <inheritdoc/>
    public string? KeyStorePassword => _baseProperties.TryGet(DestinationProperty.KeyStorePassword, out var v) ? v : null;

    /// <inheritdoc/>
    public bool IsTrustingAllCertificates =>
        _baseProperties.TryGet(DestinationProperty.TrustAll, out var trustAll)
            ? trustAll
            : _baseProperties.TryGet(DestinationProperty.TrustAllFallback, out var fallback) && fallback;

    /// <inheritdoc/>
    public X509Certificate2Collection? TrustStore => _trustStore;

    /// <inheritdoc/>
    public string? TrustStorePassword => _baseProperties.TryGet(DestinationProperty.TrustStorePassword, out var v) ? v : null;

    /// <inheritdoc/>
    public SecurityConfigurationStrategy SecurityConfigurationStrategy =>
        _baseProperties.GetOrDefault(DestinationProperty.SecurityConfiguration, SecurityConfigurationStrategyExtensions.Default);

    /// <inheritdoc/>
    public BasicCredentials? BasicCredentials => _basicCredentials;

    /// <inheritdoc/>
    public AuthType AuthenticationType => _authenticationType;

    /// <inheritdoc/>
    public ProxyKind? ProxyType => _proxyType;

    /// <summary>Creates a builder for a destination with the given URL.</summary>
    /// <param name="uri">The URL.</param>
    /// <returns>The builder.</returns>
    public static Builder CreateBuilder(string uri) => new Builder().Uri(uri);

    /// <summary>Creates a builder for a destination with the given URL.</summary>
    /// <param name="uri">The URL.</param>
    /// <returns>The builder.</returns>
    public static Builder CreateBuilder(Uri uri) => new Builder().Uri(uri);

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

    /// <summary>Creates a builder with the properties of the given object.</summary>
    /// <param name="properties">The properties.</param>
    /// <returns>The builder; for <see cref="DefaultDestination"/> and <see cref="DefaultHttpDestination"/> everything is copied.</returns>
    public static Builder FromProperties(IDestinationProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        if (properties is DefaultDestination or DefaultHttpDestination)
        {
            return FromDestination((IDestination)properties);
        }
        return CopyProperties(properties, new Builder());
    }

    /// <summary>Creates a builder from a destination.</summary>
    /// <param name="destination">A <see cref="DefaultDestination"/> or <see cref="DefaultHttpDestination"/>.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="ArgumentException">The destination is of another type.</exception>
    public static Builder FromDestination(IDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (destination is not (DefaultDestination or DefaultHttpDestination))
        {
            throw new ArgumentException(
                "The provided destination is not supported. Only DefaultDestination and DefaultHttpDestination are supported.",
                nameof(destination));
        }
        var builder = CopyProperties(destination, new Builder());
        if (destination is DefaultHttpDestination http)
        {
            builder.Headers(http.CustomHeaders);
            builder.HeaderProviders(http.CustomHeaderProviders);
            builder.KeyStoreSupplier(http._keyStoreSupplier);
            if (http._trustStore is not null)
            {
                builder.TrustStore(http._trustStore);
            }
        }
        return builder;
    }

    /// <summary>Whether a destination has a valid <c>URL</c> property, so that it can be turned into an HTTP destination.</summary>
    /// <param name="destination">The properties.</param>
    /// <returns><see langword="true"/> if the URL exists and is a valid absolute or relative URI.</returns>
    public static bool CanBeConstructedFrom(IDestinationProperties destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return destination.TryGet(DestinationProperty.Uri, out var uri) && UriHelper.TryParseDestinationUri(uri, out _);
    }

    /// <summary>
    /// Collects the headers of the custom and registered header providers.
    /// </summary>
    /// <param name="destination">The destination.</param>
    /// <param name="requestUri">The request URI.</param>
    /// <param name="customHeaderProviders">The providers attached to the destination.</param>
    /// <param name="registeredHeaderProviders">The globally registered providers.</param>
    /// <returns>The headers, custom providers first.</returns>
    /// <exception cref="DestinationAccessException">A provider threw an exception.</exception>
    public static IReadOnlyList<Header> GetHeadersFromHeaderProviders(
        IHttpDestination destination,
        Uri requestUri,
        IReadOnlyList<IDestinationHeaderProvider> customHeaderProviders,
        IReadOnlyList<IDestinationHeaderProvider> registeredHeaderProviders)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(requestUri);
        ArgumentNullException.ThrowIfNull(customHeaderProviders);
        ArgumentNullException.ThrowIfNull(registeredHeaderProviders);

        IReadOnlyList<IDestinationHeaderProvider> providers = [.. customHeaderProviders, .. registeredHeaderProviders];
        if (Log.IsEnabled(LogLevel.Debug))
        {
            Log.LogDebug(
                "Found these {Count} destination header providers: {Providers}",
                providers.Count,
                string.Join(", ", providers.Select(p => p.GetType().Name)));
        }
        var requestContext = new DestinationRequestContext(destination, requestUri);
        var result = new List<Header>();
        foreach (var provider in providers)
        {
            try
            {
                result.AddRange(provider.GetHeaders(requestContext));
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                throw new DestinationAccessException(
                    $"Header provider '{provider.GetType().Name}' threw an exception: {e.Message}",
                    e);
            }
        }
        return result;
    }

    /// <inheritdoc/>
    public object? GetProperty(string key) => _baseProperties.GetProperty(key);

    /// <inheritdoc/>
    public IReadOnlyList<Header> GetHeaders(Uri requestUri)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        var allHeaders = new List<Header>(CustomHeaders);
        allHeaders.AddRange(GetHeadersFromHeaderProviders(this, requestUri, _customHeaderProviders, _registeredHeaderProviders));
        allHeaders.AddRange(_headersFromProperties);
        if (!allHeaders.Any(h => string.Equals(h.Name, "Authorization", StringComparison.OrdinalIgnoreCase)))
        {
            allHeaders.AddRange(GetHeadersForAuthType());
        }
        if (!allHeaders.Any(h => string.Equals(h.Name, "Proxy-Authorization", StringComparison.OrdinalIgnoreCase)))
        {
            allHeaders.AddRange(_proxyAuthorizationHeaders);
        }
        return allHeaders;
    }

    /// <summary>Creates a builder initialized with this destination.</summary>
    /// <returns>The builder.</returns>
    public Builder ToBuilder() => FromDestination(this);

    /// <summary>Prints name, URI, proxy type and authentication type only; other properties may be confidential.</summary>
    /// <returns>A string representation.</returns>
    public override string ToString()
    {
        var sb = new StringBuilder("DefaultHttpDestination(");
        if (_baseProperties.TryGet(DestinationProperty.Name, out var name))
        {
            sb.Append("name=").Append(name).Append(", ");
        }
        if (_baseProperties.TryGet(DestinationProperty.Uri, out var uri))
        {
            sb.Append("uri=").Append(WithoutUserInfo(uri)).Append(", ");
        }
        if (_baseProperties.TryGet(DestinationProperty.ProxyType, out var proxyType))
        {
            sb.Append("proxyType=").Append(proxyType.GetIdentifier()).Append(", ");
        }
        if (_baseProperties.TryGet(DestinationProperty.AuthType, out var authType))
        {
            sb.Append("authType=").Append(authType.GetIdentifier()).Append(", ");
        }
        return sb.Append("...)").ToString();
    }

    /// <inheritdoc/>
    public bool Equals(DefaultHttpDestination? other)
    {
        if (other is null)
        {
            return false;
        }
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        if (_registeredHeaderProviders.Count != other._registeredHeaderProviders.Count
            || _customHeaderProviders.Count != other._customHeaderProviders.Count)
        {
            return false;
        }
        return _baseProperties.Equals(other._baseProperties)
            && CustomHeaders.SequenceEqual(other.CustomHeaders)
            && DestinationKeyStoreComparator.KeyStoresEqual(_keyStoreSupplier(), other._keyStoreSupplier())
            && DestinationKeyStoreComparator.KeyStoresEqual(_trustStore, other._trustStore)
            && _customHeaderProviders.SequenceEqual(other._customHeaderProviders)
            && _registeredHeaderProviders.SequenceEqual(other._registeredHeaderProviders);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as DefaultHttpDestination);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_baseProperties);
        foreach (var header in CustomHeaders)
        {
            hash.Add(header);
        }
        hash.Add(DestinationKeyStoreComparator.ResolveKeyStoreHashCode(_keyStoreSupplier()));
        hash.Add(DestinationKeyStoreComparator.ResolveKeyStoreHashCode(_trustStore));
        foreach (var provider in _customHeaderProviders)
        {
            hash.Add(provider);
        }
        foreach (var provider in _registeredHeaderProviders)
        {
            hash.Add(provider);
        }
        return hash.ToHashCode();
    }

    private static Builder CopyProperties(IDestinationProperties source, Builder builder)
    {
        foreach (var name in source.PropertyNames)
        {
            var value = source.GetProperty(name);
            if (value is not null)
            {
                builder.Property(name, value);
            }
        }
        return builder;
    }

    private static Uri ParseUri(IDestinationProperties properties)
    {
        properties.TryGet(DestinationProperty.Uri, out var value);
        return UriHelper.TryParseDestinationUri(value!, out var uri) ? uri : throw new UnreachableException();
    }

    private static string WithoutUserInfo(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.UserInfo.Length > 0
            ? uri.Replace(parsed.UserInfo + "@", string.Empty, StringComparison.Ordinal)
            : uri;

    private List<Header> GetHeadersForAuthType()
    {
        var headers = new List<Header>();
        switch (_authenticationType)
        {
            case AuthType.BasicAuthentication:
                if (_basicCredentials is null)
                {
                    throw new DestinationAccessException(
                        "Failed to add 'Authorization' header for Basic authentication: no credentials available.");
                }
                headers.Add(new Header("Authorization", "Basic " + BasicAuthHeaderEncoder.EncodeUserPasswordBase64(_basicCredentials)));
                break;
            case AuthType.TokenForwarding:
                var requestHeaders = RequestHeaderAccessor.TryGetHeaderContainer();
                var toAdd = (requestHeaders.IsSuccess ? requestHeaders.GetValueOrDefault() : null)
                    ?.GetHeaderValues("Authorization")
                    .Where(v => v is not null)
                    .Select(v => new Header("Authorization", v))
                    .ToList() ?? [];
                if (toAdd.Count == 0)
                {
                    if (Log.IsEnabled(LogLevel.Warning))
                    {
                        Log.LogWarning(
                            "Did not find any 'Authorization' headers to add to the outgoing request, even though Authentication type '{AuthType}' is set.",
                            AuthType.TokenForwarding.GetIdentifier());
                    }
                    if (Log.IsEnabled(LogLevel.Debug))
                    {
                        if (requestHeaders.IsFailure || requestHeaders.GetValueOrDefault() is null)
                        {
                            if (Log.IsEnabled(LogLevel.Debug))
                            {
                                Log.LogDebug(requestHeaders.Exception, "The incoming request headers could not be accessed.");
                            }
                        }
                        else
                        {
                            if (Log.IsEnabled(LogLevel.Debug))
                            {
                                Log.LogDebug(
                                    "Unable to find an 'Authorization' header in the following headers: {Headers}",
                                    string.Join(", ", requestHeaders.GetValueOrDefault()!.HeaderNames));
                            }
                        }
                    }
                }
                headers.AddRange(toAdd);
                break;
            default:
                break;
        }
        return headers;
    }

    /// <summary>Builds <see cref="DefaultHttpDestination"/>s.</summary>
    public sealed class Builder
    {
        private readonly List<Header> _headers = [];
        private readonly DefaultDestination.Builder _builder = DefaultDestination.CreateBuilder();
        private readonly List<IDestinationHeaderProvider> _customHeaderProviders = [];
        private Func<X509Certificate2Collection?> _keyStoreSupplier = static () => null;
        private X509Certificate2Collection? _trustStore;

        internal Builder()
        {
        }

        internal DefaultHttpDestinationBuilderProxyHandler ProxyHandler { get; set; } = new();

        internal IReadOnlyList<IDestinationHeaderProvider> CustomHeaderProvidersInternal => _customHeaderProviders;

        /// <summary>Sets the key store holding the client certificate.</summary>
        /// <param name="keyStore">The key store, or <see langword="null"/> for none.</param>
        /// <returns>This builder.</returns>
        public Builder KeyStore(X509Certificate2Collection? keyStore)
        {
            _keyStoreSupplier = () => keyStore;
            return this;
        }

        /// <summary>Sets a function that provides the key store on demand.</summary>
        /// <param name="supplier">The supplier.</param>
        /// <returns>This builder.</returns>
        public Builder KeyStoreSupplier(Func<X509Certificate2Collection?> supplier)
        {
            ArgumentNullException.ThrowIfNull(supplier);
            _keyStoreSupplier = supplier;
            return this;
        }

        /// <summary>Sets the trust store.</summary>
        /// <param name="trustStore">The trust store, or <see langword="null"/> for none.</param>
        /// <returns>This builder.</returns>
        public Builder TrustStore(X509Certificate2Collection? trustStore)
        {
            _trustStore = trustStore;
            return this;
        }

        /// <summary>Sets a property.</summary>
        /// <param name="key">The property name.</param>
        /// <param name="value">The value.</param>
        /// <returns>This builder.</returns>
        public Builder Property(string key, object value)
        {
            _builder.Property(key, value);
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

        /// <summary>Reads a property that has been set on the builder.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="key">The typed key.</param>
        /// <param name="value">The converted value.</param>
        /// <returns><see langword="true"/> if the property is set.</returns>
        public bool TryGet<T>(DestinationPropertyKey<T> key, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out T value) =>
            _builder.TryGet(key, out value);

        /// <summary>Removes a property.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="key">The typed key.</param>
        /// <returns>This builder.</returns>
        public Builder RemoveProperty<T>(DestinationPropertyKey<T> key)
        {
            _builder.RemoveProperty(key);
            return this;
        }

        /// <summary>Removes a property.</summary>
        /// <param name="key">The property name.</param>
        /// <returns>This builder.</returns>
        public Builder RemoveProperty(string key)
        {
            _builder.RemoveProperty(key);
            return this;
        }

        /// <summary>Sets the destination name.</summary>
        /// <param name="name">The name.</param>
        /// <returns>This builder.</returns>
        public Builder Name(string name) => Property(DestinationProperty.Name, name);

        /// <summary>Sets the destination URL.</summary>
        /// <param name="uri">The URL.</param>
        /// <returns>This builder.</returns>
        public Builder Uri(Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);
            return Uri(uri.OriginalString);
        }

        /// <summary>Sets the destination URL.</summary>
        /// <param name="uri">The URL.</param>
        /// <returns>This builder.</returns>
        public Builder Uri(string uri) => Property(DestinationProperty.Uri, uri);

        /// <summary>Sets the TLS version.</summary>
        /// <param name="value">The version, for example <c>TLSv1.3</c>.</param>
        /// <returns>This builder.</returns>
        public Builder TlsVersion(string value) => Property(DestinationProperty.TlsVersion, value);

        /// <summary>Sets the key store password.</summary>
        /// <param name="value">The password.</param>
        /// <returns>This builder.</returns>
        public Builder KeyStorePassword(string value) => Property(DestinationProperty.KeyStorePassword, value);

        /// <summary>Disables server certificate validation. Insecure: use only for development.</summary>
        /// <returns>This builder.</returns>
        public Builder TrustAllCertificates() => Property(DestinationProperty.TrustAll, true);

        /// <summary>Sets the trust store password.</summary>
        /// <param name="value">The password.</param>
        /// <returns>This builder.</returns>
        public Builder TrustStorePassword(string value) => Property(DestinationProperty.TrustStorePassword, value);

        /// <summary>Sets the Cloud Connector location ID.</summary>
        /// <param name="locationId">The location ID.</param>
        /// <returns>This builder.</returns>
        public Builder CloudConnectorLocationId(string locationId) =>
            Property(DestinationProperty.CloudConnectorLocationId, locationId);

        /// <summary>Sets the proxy, including its credentials.</summary>
        /// <param name="value">The proxy configuration.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentException">The credentials are neither Basic, Bearer nor none.</exception>
        public Builder ProxyConfiguration(ProxyConfiguration value)
        {
            ArgumentNullException.ThrowIfNull(value);
            SetProxyAuth(value.Credentials);
            return Property(DestinationProperty.ProxyUri, value.Uri);
        }

        /// <summary>Adds headers.</summary>
        /// <param name="headers">The headers.</param>
        /// <returns>This builder.</returns>
        public Builder Headers(IEnumerable<Header> headers)
        {
            ArgumentNullException.ThrowIfNull(headers);
            _headers.AddRange(headers);
            return this;
        }

        /// <summary>Sets the proxy URI.</summary>
        /// <param name="proxyUri">The proxy URI.</param>
        /// <returns>This builder.</returns>
        public Builder Proxy(Uri proxyUri) => Property(DestinationProperty.ProxyUri, proxyUri);

        /// <summary>Sets the proxy by host and port.</summary>
        /// <param name="proxyHost">The host.</param>
        /// <param name="proxyPort">The port.</param>
        /// <returns>This builder.</returns>
        public Builder Proxy(string proxyHost, int proxyPort) =>
            Property(DestinationProperty.ProxyHost, proxyHost).Property(DestinationProperty.ProxyPort, proxyPort);

        /// <summary>Sets the value of the <c>Proxy-Authorization</c> header.</summary>
        /// <param name="proxyAuthorization">The header value; confidential.</param>
        /// <returns>This builder.</returns>
        public Builder ProxyAuthorization(string proxyAuthorization) =>
            Property(DestinationProperty.ProxyAuth, proxyAuthorization);

        /// <summary>Sets the proxy type.</summary>
        /// <param name="proxyType">The proxy type.</param>
        /// <returns>This builder.</returns>
        public Builder ProxyType(ProxyKind proxyType) => Property(DestinationProperty.ProxyType, proxyType);

        /// <summary>Sets Basic credentials and the Basic authentication type.</summary>
        /// <param name="basicCredentials">The credentials.</param>
        /// <returns>This builder.</returns>
        public Builder BasicCredentials(BasicCredentials basicCredentials)
        {
            ArgumentNullException.ThrowIfNull(basicCredentials);
            return BasicCredentials(basicCredentials.Username, basicCredentials.Password);
        }

        /// <summary>Sets Basic credentials and the Basic authentication type.</summary>
        /// <param name="user">The user.</param>
        /// <param name="password">The password.</param>
        /// <returns>This builder.</returns>
        public Builder BasicCredentials(string user, string password) =>
            AuthenticationType(AuthType.BasicAuthentication)
                .Property(DestinationProperty.BasicAuthUsername, user)
                .Property(DestinationProperty.BasicAuthPassword, password);

        /// <summary>Sets the authentication type.</summary>
        /// <param name="authenticationType">The type.</param>
        /// <returns>This builder.</returns>
        public Builder AuthenticationType(AuthType authenticationType) =>
            Property(DestinationProperty.AuthType, authenticationType);

        /// <summary>Adds a header.</summary>
        /// <param name="header">The header.</param>
        /// <returns>This builder.</returns>
        public Builder Header(Header header)
        {
            ArgumentNullException.ThrowIfNull(header);
            _headers.Add(header);
            return this;
        }

        /// <summary>Adds a header.</summary>
        /// <param name="headerName">The name.</param>
        /// <param name="headerValue">The value.</param>
        /// <returns>This builder.</returns>
        public Builder Header(string headerName, string headerValue) => Header(new Header(headerName, headerValue));

        /// <summary>Sets where the TLS configuration comes from.</summary>
        /// <param name="strategy">The strategy.</param>
        /// <returns>This builder.</returns>
        public Builder SecurityConfiguration(SecurityConfigurationStrategy strategy) =>
            Property(DestinationProperty.SecurityConfiguration, strategy);

        /// <summary>Adds header providers.</summary>
        /// <param name="headerProviders">The providers.</param>
        /// <returns>This builder.</returns>
        public Builder HeaderProviders(params IEnumerable<IDestinationHeaderProvider> headerProviders)
        {
            ArgumentNullException.ThrowIfNull(headerProviders);
            _customHeaderProviders.AddRange(headerProviders);
            return this;
        }

        /// <summary>Creates the destination.</summary>
        /// <returns>The destination. For on-premise proxy types it is resolved via the registered service binding destination loaders.</returns>
        /// <exception cref="ArgumentException">There is no URL, or it is invalid.</exception>
        /// <exception cref="DestinationAccessException">The proxy configuration cannot be resolved.</exception>
        public DefaultHttpDestination Build()
        {
            if (!TryGet(DestinationProperty.Uri, out _))
            {
                throw new ArgumentException("Cannot build a HttpDestination without a URL.");
            }
            if (!TryGet(DestinationProperty.Type, out _))
            {
                Property(DestinationProperty.Type, DestinationType.Http);
            }
            if (ProxyHandler.CanHandle(this))
            {
                try
                {
                    return ProxyHandler.Handle(this);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    throw new DestinationAccessException("Unable to resolve proxy configuration for destination", e);
                }
            }
            return BuildInternal();
        }

        internal DefaultHttpDestination BuildInternal() =>
            new(
                _builder.Build(),
                _headers,
                _keyStoreSupplier,
                _trustStore,
                _customHeaderProviders);

        private void SetProxyAuth(ISecurityCredentials? credentials)
        {
            switch (credentials)
            {
                case null or NoCredentials:
                    RemoveProperty(DestinationProperty.ProxyAuth);
                    break;
                case BasicCreds basic:
                    Property(DestinationProperty.ProxyAuth, basic.HttpHeaderValue);
                    break;
                case BearerCredentials bearer:
                    Property(DestinationProperty.ProxyAuth, bearer.HttpHeaderValue);
                    break;
                default:
                    throw new ArgumentException(
                        $"The provided proxy credentials ({credentials.GetType().Name}) are not supported. "
                        + "Consider adding the Proxy-Authorization header yourself using the `Header` method.");
            }
        }
    }
}
