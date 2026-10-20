using System.Security.Cryptography.X509Certificates;
using Sap.Cloud.Sdk.CloudPlatform.Security;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// A destination that sends requests to a transparent proxy (destination gateway) instead of directly to the target
/// system. The gateway resolves the destination by the name given in the <c>x-destination-name</c> header and performs the
/// authentication flows itself, so this destination has no authentication, proxy or TLS configuration of its own.
/// </summary>
/// <remarks>
/// <para>
/// Port of <c>TransparentProxyDestination</c>. Create instances with <see cref="CreateBuilder(string)"/> (a plain proxy
/// destination) or <see cref="CreateGatewayBuilder(string, string)"/> (a gateway destination for a named destination).
/// </para>
/// <para>
/// Headers sent with every request: the headers set on the builder, the headers of header providers, then per request
/// <c>x-tenant-id</c> (the current tenant, unless a tenant id or subdomain header was set) and <c>authorization</c> (the raw
/// JWT of the current user, unless an <c>authorization</c> header was set). Header values are hidden from <c>ToString</c>.
/// </para>
/// </remarks>
public sealed class TransparentProxyDestination : IHttpDestination, IEquatable<TransparentProxyDestination>
{
    internal const string DestinationNameHeaderKey = "x-destination-name";
    internal const string FragmentNameHeaderKey = "x-fragment-name";
    internal const string TenantSubdomainHeaderKey = "x-tenant-subdomain";
    internal const string TenantIdHeaderKey = "x-tenant-id";
    internal const string FragmentOptionalHeaderKey = "x-fragment-optional";
    internal const string DestinationLevelHeaderKey = "x-destination-level";
    internal const string FragmentLevelHeaderKey = "x-fragment-level";
    internal const string TokenServiceTenantHeaderKey = "x-token-service-tenant";
    internal const string ClientAssertionHeaderKey = "x-client-assertion";
    internal const string ClientAssertionTypeHeaderKey = "x-client-assertion-type";
    internal const string ClientAssertionDestinationNameHeaderKey = "x-client-assertion-destination-name";
    internal const string AuthorizationHeaderKey = "authorization";
    internal const string SubjectTokenTypeHeaderKey = "x-subject-token-type";
    internal const string ActorTokenHeaderKey = "x-actor-token";
    internal const string ActorTokenTypeHeaderKey = "x-actor-token-type";
    internal const string RedirectUriHeaderKey = "x-redirect-uri";
    internal const string CodeVerifierHeaderKey = "x-code-verifier";
    internal const string ChainNameHeaderKey = "x-chain-name";
    internal const string ChainVarSubjectTokenHeaderKey = "x-chain-var-subjectToken";
    internal const string ChainVarSubjectTokenTypeHeaderKey = "x-chain-var-subjectTokenType";
    internal const string ChainVarSamlProviderDestinationNameHeaderKey = "x-chain-var-samlProviderDestinationName";
    internal const string TenantIdAndTenantSubdomainBothPassedErrorMessage =
        "Tenant id and tenant subdomain cannot be passed at the same time.";

    private readonly DefaultDestination _baseProperties;
    private readonly IReadOnlyList<Header> _customHeaders;
    private readonly IReadOnlyList<IDestinationHeaderProvider> _customHeaderProviders;
    private readonly IReadOnlyList<IDestinationHeaderProvider> _registeredHeaderProviders;

    private TransparentProxyDestination(
        DefaultDestination baseProperties,
        IEnumerable<Header> customHeaders,
        IEnumerable<IDestinationHeaderProvider> customHeaderProviders)
    {
        _baseProperties = baseProperties;
        _customHeaders = [.. customHeaders];
        _customHeaderProviders = [.. customHeaderProviders];
        _registeredHeaderProviders = DestinationHeaderProviders.Registered;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<string> PropertyNames => _baseProperties.PropertyNames;

    /// <summary>The URI of the transparent proxy.</summary>
    public Uri Uri =>
        _baseProperties.TryGet(DestinationProperty.Uri, out var uri)
            ? new Uri(uri, UriKind.RelativeOrAbsolute)
            : throw new InvalidOperationException("The transparent proxy destination has no URL.");

    /// <inheritdoc/>
    public string? TlsVersion => null;

    /// <inheritdoc/>
    public ProxyConfiguration? ProxyConfiguration => null;

    /// <inheritdoc/>
    public X509Certificate2Collection? KeyStore => null;

    /// <inheritdoc/>
    public string? KeyStorePassword => null;

    /// <inheritdoc/>
    public bool IsTrustingAllCertificates => false;

    /// <inheritdoc/>
    public BasicCredentials? BasicCredentials => null;

    /// <inheritdoc/>
    public AuthenticationType AuthenticationType => AuthenticationType.NoAuthentication;

    /// <inheritdoc/>
    public ProxyType? ProxyType => null;

    /// <inheritdoc/>
    public X509Certificate2Collection? TrustStore => null;

    /// <inheritdoc/>
    public string? TrustStorePassword => null;

    internal IReadOnlyList<Header> CustomHeaders => _customHeaders;

    internal IReadOnlyList<IDestinationHeaderProvider> CustomHeaderProviders => _customHeaderProviders;

    /// <summary>Creates a builder for a proxy destination.</summary>
    /// <param name="uri">The URI of the transparent proxy.</param>
    /// <returns>The builder.</returns>
    public static Builder CreateBuilder(string uri) => new(uri);

    /// <summary>Creates a builder for a destination that the gateway resolves by name.</summary>
    /// <param name="destinationName">The name of the destination, sent as <c>x-destination-name</c>.</param>
    /// <param name="uri">The URI of the gateway.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="ArgumentException">The destination name is empty.</exception>
    public static GatewayBuilder CreateGatewayBuilder(string destinationName, string uri) => new(destinationName, uri);

    internal static bool ContainsHeader(IEnumerable<Header> headers, string headerName) =>
        headers.Any(h => string.Equals(h.Name, headerName, StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc/>
    public object? GetProperty(string key) => _baseProperties.GetProperty(key);

    /// <inheritdoc/>
    public IReadOnlyList<Header> GetHeaders(Uri requestUri)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        return AddContextHeaders(
            DefaultHttpDestination.GetHeadersFromHeaderProviders(this, requestUri, _customHeaderProviders, _registeredHeaderProviders));
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Header>> GetHeadersAsync(Uri requestUri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestUri);
        return AddContextHeaders(
            await DefaultHttpDestination.GetHeadersFromHeaderProvidersAsync(
                this, requestUri, _customHeaderProviders, _registeredHeaderProviders, cancellationToken).ConfigureAwait(false));
    }

    private List<Header> AddContextHeaders(IReadOnlyList<Header> providerHeaders)
    {
        List<Header> allHeaders = [.. _customHeaders, .. providerHeaders];

        // Automatically add tenant id if not already present
        var tenant = TenantAccessor.TryGetCurrentTenant();
        if (tenant.IsSuccess
            && !ContainsHeader(allHeaders, TenantIdHeaderKey)
            && !ContainsHeader(allHeaders, TenantSubdomainHeaderKey))
        {
            allHeaders.Add(new Header(TenantIdHeaderKey, tenant.GetValueOrThrow().TenantId));
        }

        var token = AuthTokenAccessor.TryGetCurrentToken();
        if (token.IsSuccess && !ContainsHeader(allHeaders, AuthorizationHeaderKey))
        {
            allHeaders.Add(new Header(AuthorizationHeaderKey, token.GetValueOrThrow().Jwt.EncodedToken));
        }

        return allHeaders;
    }

    /// <inheritdoc/>
    public bool Equals(TransparentProxyDestination? other) =>
        other is not null
        && (ReferenceEquals(this, other)
            || (_baseProperties.Equals(other._baseProperties) && _customHeaders.SequenceEqual(other._customHeaders)));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as TransparentProxyDestination);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(_baseProperties);
        foreach (var header in _customHeaders)
        {
            hash.Add(header);
        }
        return hash.ToHashCode();
    }

    /// <summary>Prints the URI and the names of the headers, never their values.</summary>
    /// <returns>A string representation.</returns>
    public override string ToString() =>
        $"TransparentProxyDestination(Uri={Uri}, Headers=[{string.Join(", ", _customHeaders.Select(h => h.Name))}])";

    /// <summary>Builds <see cref="TransparentProxyDestination"/>s.</summary>
    /// <typeparam name="TBuilder">The type of the concrete builder.</typeparam>
    public abstract class AbstractBuilder<TBuilder>
        where TBuilder : AbstractBuilder<TBuilder>
    {
        private readonly List<Header> _headers = [];
        private readonly List<IDestinationHeaderProvider> _customHeaderProviders = [];
        private readonly DefaultDestination.Builder _propertiesBuilder = DefaultDestination.CreateBuilder();

        internal AbstractBuilder()
        {
        }

        /// <summary>This builder, typed.</summary>
        protected abstract TBuilder This { get; }

        /// <summary>Sets a property of the destination.</summary>
        /// <param name="key">The name of the property.</param>
        /// <param name="value">The value.</param>
        /// <returns>This builder.</returns>
        public TBuilder Property(string key, object value)
        {
            _propertiesBuilder.Property(key, value);
            return This;
        }

        /// <summary>Sets a property of the destination.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="key">The typed key.</param>
        /// <param name="value">The value.</param>
        /// <returns>This builder.</returns>
        public TBuilder Property<T>(DestinationPropertyKey<T> key, T value)
            where T : notnull
        {
            ArgumentNullException.ThrowIfNull(key);
            return Property(key.KeyName, value);
        }

        /// <summary>Adds a header provider.</summary>
        /// <param name="headerProvider">The provider.</param>
        /// <returns>This builder.</returns>
        public TBuilder HeaderProvider(IDestinationHeaderProvider headerProvider)
        {
            ArgumentNullException.ThrowIfNull(headerProvider);
            _customHeaderProviders.Add(headerProvider);
            return This;
        }

        /// <summary>Adds headers.</summary>
        /// <param name="headers">The headers.</param>
        /// <returns>This builder.</returns>
        public TBuilder Headers(IEnumerable<Header> headers)
        {
            ArgumentNullException.ThrowIfNull(headers);
            _headers.AddRange(headers);
            return This;
        }

        /// <summary>Adds a header.</summary>
        /// <param name="header">The header.</param>
        /// <returns>This builder.</returns>
        public TBuilder Header(Header header)
        {
            ArgumentNullException.ThrowIfNull(header);
            _headers.Add(header);
            return This;
        }

        /// <summary>Adds a header.</summary>
        /// <param name="headerName">The name.</param>
        /// <param name="headerValue">The value.</param>
        /// <returns>This builder.</returns>
        public TBuilder Header(string headerName, string headerValue) => Header(new Header(headerName, headerValue));

        /// <summary>Sets the subdomain of the tenant (<c>x-tenant-subdomain</c>).</summary>
        /// <param name="tenantSubdomain">The subdomain.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="InvalidOperationException">A tenant id has been set already.</exception>
        public TBuilder TenantSubdomain(string tenantSubdomain)
        {
            if (ContainsHeader(_headers, TenantIdHeaderKey))
            {
                throw new InvalidOperationException(TenantIdAndTenantSubdomainBothPassedErrorMessage);
            }
            return Header(TenantSubdomainHeaderKey, tenantSubdomain);
        }

        /// <summary>Sets the id of the tenant (<c>x-tenant-id</c>).</summary>
        /// <param name="tenantId">The tenant id.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="InvalidOperationException">A tenant subdomain has been set already.</exception>
        public TBuilder TenantId(string tenantId)
        {
            if (ContainsHeader(_headers, TenantSubdomainHeaderKey))
            {
                throw new InvalidOperationException(TenantIdAndTenantSubdomainBothPassedErrorMessage);
            }
            return Header(TenantIdHeaderKey, tenantId);
        }

        /// <summary>Sets the tenant of the token service (<c>x-token-service-tenant</c>).</summary>
        /// <param name="tokenServiceTenant">The tenant.</param>
        /// <returns>This builder.</returns>
        public TBuilder TokenServiceTenant(string tokenServiceTenant) => Header(TokenServiceTenantHeaderKey, tokenServiceTenant);

        /// <summary>Sets the client assertion (<c>x-client-assertion</c>). Confidential.</summary>
        /// <param name="clientAssertion">The assertion.</param>
        /// <returns>This builder.</returns>
        public TBuilder ClientAssertion(string clientAssertion) => Header(ClientAssertionHeaderKey, clientAssertion);

        /// <summary>Sets the client assertion type (<c>x-client-assertion-type</c>).</summary>
        /// <param name="clientAssertionType">The type.</param>
        /// <returns>This builder.</returns>
        public TBuilder ClientAssertionType(string clientAssertionType) => Header(ClientAssertionTypeHeaderKey, clientAssertionType);

        /// <summary>Sets the name of the destination that provides the client assertion (<c>x-client-assertion-destination-name</c>).</summary>
        /// <param name="clientAssertionDestinationName">The name.</param>
        /// <returns>This builder.</returns>
        public TBuilder ClientAssertionDestinationName(string clientAssertionDestinationName) =>
            Header(ClientAssertionDestinationNameHeaderKey, clientAssertionDestinationName);

        /// <summary>Sets the authorization (<c>authorization</c>). Confidential.</summary>
        /// <param name="authorization">The authorization.</param>
        /// <returns>This builder.</returns>
        public TBuilder Authorization(string authorization) => Header(AuthorizationHeaderKey, authorization);

        /// <summary>Sets the subject token type (<c>x-subject-token-type</c>).</summary>
        /// <param name="subjectTokenType">The type.</param>
        /// <returns>This builder.</returns>
        public TBuilder SubjectTokenType(string subjectTokenType) => Header(SubjectTokenTypeHeaderKey, subjectTokenType);

        /// <summary>Sets the actor token (<c>x-actor-token</c>). Confidential.</summary>
        /// <param name="actorToken">The token.</param>
        /// <returns>This builder.</returns>
        public TBuilder ActorToken(string actorToken) => Header(ActorTokenHeaderKey, actorToken);

        /// <summary>Sets the actor token type (<c>x-actor-token-type</c>).</summary>
        /// <param name="actorTokenType">The type.</param>
        /// <returns>This builder.</returns>
        public TBuilder ActorTokenType(string actorTokenType) => Header(ActorTokenTypeHeaderKey, actorTokenType);

        /// <summary>Sets the redirect URI (<c>x-redirect-uri</c>).</summary>
        /// <param name="redirectUri">The URI.</param>
        /// <returns>This builder.</returns>
        public TBuilder RedirectUri(string redirectUri) => Header(RedirectUriHeaderKey, redirectUri);

        /// <summary>Sets the PKCE code verifier (<c>x-code-verifier</c>). Confidential.</summary>
        /// <param name="codeVerifier">The verifier.</param>
        /// <returns>This builder.</returns>
        public TBuilder CodeVerifier(string codeVerifier) => Header(CodeVerifierHeaderKey, codeVerifier);

        /// <summary>Sets the name of the destination chain (<c>x-chain-name</c>).</summary>
        /// <param name="chainName">The name.</param>
        /// <returns>This builder.</returns>
        public TBuilder ChainName(string chainName) => Header(ChainNameHeaderKey, chainName);

        /// <summary>Sets the subject token of a chain variable (<c>x-chain-var-subjectToken</c>). Confidential.</summary>
        /// <param name="subjectToken">The token.</param>
        /// <returns>This builder.</returns>
        public TBuilder ChainVarSubjectToken(string subjectToken) => Header(ChainVarSubjectTokenHeaderKey, subjectToken);

        /// <summary>Sets the subject token type of a chain variable (<c>x-chain-var-subjectTokenType</c>).</summary>
        /// <param name="subjectTokenType">The type.</param>
        /// <returns>This builder.</returns>
        public TBuilder ChainVarSubjectTokenType(string subjectTokenType) =>
            Header(ChainVarSubjectTokenTypeHeaderKey, subjectTokenType);

        /// <summary>Sets the SAML provider destination of a chain variable (<c>x-chain-var-samlProviderDestinationName</c>).</summary>
        /// <param name="samlProviderDestinationName">The name.</param>
        /// <returns>This builder.</returns>
        public TBuilder ChainVarSamlProviderDestinationName(string samlProviderDestinationName) =>
            Header(ChainVarSamlProviderDestinationNameHeaderKey, samlProviderDestinationName);

        /// <summary>Creates the destination.</summary>
        /// <returns>The destination.</returns>
        public TransparentProxyDestination Build() =>
            new(_propertiesBuilder.Build(), _headers, _customHeaderProviders);
    }

    /// <summary>Builds a <see cref="TransparentProxyDestination"/> for a plain proxy.</summary>
    public sealed class Builder : AbstractBuilder<Builder>
    {
        internal Builder(string uri) => Property(DestinationProperty.Uri, uri);

        /// <inheritdoc/>
        protected override Builder This => this;
    }

    /// <summary>Builds a <see cref="TransparentProxyDestination"/> for a destination that the gateway resolves by name.</summary>
    public sealed class GatewayBuilder : AbstractBuilder<GatewayBuilder>
    {
        internal GatewayBuilder(string destinationName, string uri)
        {
            ArgumentNullException.ThrowIfNull(destinationName);
            if (destinationName.Length == 0)
            {
                throw new ArgumentException(
                    "The 'destinationName' property is required for destination-gateway but was not set.",
                    nameof(destinationName));
            }

            Header(DestinationNameHeaderKey, destinationName);
            Property(DestinationProperty.Uri, uri);
        }

        /// <inheritdoc/>
        protected override GatewayBuilder This => this;

        /// <summary>Sets the fragment that enhances the destination (<c>x-fragment-name</c>).</summary>
        /// <param name="fragmentName">The fragment name.</param>
        /// <returns>This builder.</returns>
        public GatewayBuilder FragmentName(string fragmentName) => Header(FragmentNameHeaderKey, fragmentName);

        /// <summary>Sets the level at which the destination is looked up (<c>x-destination-level</c>).</summary>
        /// <param name="destinationLevel">The level.</param>
        /// <returns>This builder.</returns>
        public GatewayBuilder DestinationLevel(DestinationServiceOptionsAugmenter.CrossLevelScope destinationLevel) =>
            Header(DestinationLevelHeaderKey, destinationLevel.GetIdentifier());

        /// <summary>Sets the level at which the fragment is looked up (<c>x-fragment-level</c>).</summary>
        /// <param name="fragmentLevel">The level.</param>
        /// <returns>This builder.</returns>
        public GatewayBuilder FragmentLevel(DestinationServiceOptionsAugmenter.CrossLevelScope fragmentLevel) =>
            Header(FragmentLevelHeaderKey, fragmentLevel.GetIdentifier());

        /// <summary>Sets whether the fragment is optional (<c>x-fragment-optional</c>).</summary>
        /// <param name="fragmentOptional">Whether a missing fragment is tolerated.</param>
        /// <returns>This builder.</returns>
        public GatewayBuilder FragmentOptional(bool fragmentOptional) =>
            Header(FragmentOptionalHeaderKey, fragmentOptional ? "true" : "false");
    }
}
