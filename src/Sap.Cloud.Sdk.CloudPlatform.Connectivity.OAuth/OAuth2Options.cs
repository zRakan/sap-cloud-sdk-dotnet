using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.OAuth2;
using Sap.Cloud.Sdk.CloudPlatform.Resilience;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Controls how OAuth 2.0 tokens are retrieved for a destination created from a service binding.</summary>
public sealed class OAuth2Options
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<OAuth2Options>();

    /// <summary>The default timeout for token retrieval: 10 seconds.</summary>
    public static readonly ResilienceConfiguration.TimeLimiterConfiguration DefaultTimeout =
        ResilienceConfiguration.TimeLimiterConfiguration.Of(TimeSpan.FromSeconds(10));

    /// <summary>The default options: retrieve tokens, no extra parameters, no mTLS towards the target system.</summary>
    public static readonly OAuth2Options Default = Builder().Build();

    private readonly IReadOnlyDictionary<string, string> _additionalTokenRetrievalParameters;

    private OAuth2Options(
        bool skipTokenRetrieval,
        IReadOnlyDictionary<string, string> additionalTokenRetrievalParameters,
        ResilienceConfiguration.TimeLimiterConfiguration timeLimiter,
        Func<X509Certificate2Collection?>? clientKeyStoreSupplier,
        TokenCacheParameters tokenCacheParameters,
        Uri? btpTenantApiBaseUri)
    {
        SkipTokenRetrieval = skipTokenRetrieval;
        _additionalTokenRetrievalParameters = additionalTokenRetrievalParameters;
        TimeLimiter = timeLimiter;
        ClientKeyStoreSupplier = clientKeyStoreSupplier;
        TokenCacheParameters = tokenCacheParameters;
        BtpTenantApiBaseUri = btpTenantApiBaseUri;
    }

    /// <summary>Whether no token is retrieved at all (the target accepts requests without one).</summary>
    public bool SkipTokenRetrieval { get; }

    /// <summary>Additional form parameters for the token request.</summary>
    public IReadOnlyDictionary<string, string> AdditionalTokenRetrievalParameters =>
        new Dictionary<string, string>(_additionalTokenRetrievalParameters);

    /// <summary>The timeout for token retrieval.</summary>
    public ResilienceConfiguration.TimeLimiterConfiguration TimeLimiter { get; }

    /// <summary>The token cache configuration.</summary>
    public TokenCacheParameters TokenCacheParameters { get; }

    /// <summary>The client key store used for mTLS towards the target system, if any.</summary>
    public X509Certificate2Collection? ClientKeyStore => ClientKeyStoreSupplier?.Invoke();

    internal Func<X509Certificate2Collection?>? ClientKeyStoreSupplier { get; }

    internal Uri? BtpTenantApiBaseUri { get; }

    /// <summary>Creates a builder.</summary>
    /// <returns>The builder.</returns>
    public static OAuth2OptionsBuilder Builder() => new();

    /// <summary>Builds <see cref="OAuth2Options"/>.</summary>
    public sealed class OAuth2OptionsBuilder
    {
        private readonly Dictionary<string, string> _parameters = new(StringComparer.Ordinal);
        private bool _skipTokenRetrieval;
        private Func<X509Certificate2Collection?>? _clientKeyStoreSupplier;
        private ResilienceConfiguration.TimeLimiterConfiguration _timeLimiter = DefaultTimeout;
        private TokenCacheParameters _tokenCacheParameters = TokenCacheParameters.Default;
        private Uri? _btpTenantApiBaseUri;

        internal OAuth2OptionsBuilder()
        {
        }

        /// <summary>Skips token retrieval.</summary>
        /// <param name="skip">Whether to skip it.</param>
        /// <returns>This builder.</returns>
        public OAuth2OptionsBuilder WithSkipTokenRetrieval(bool skip)
        {
            _skipTokenRetrieval = skip;
            return this;
        }

        /// <summary>Adds a form parameter to the token request.</summary>
        /// <param name="key">The name.</param>
        /// <param name="value">The value.</param>
        /// <returns>This builder.</returns>
        public OAuth2OptionsBuilder WithTokenRetrievalParameter(string key, string value)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(value);
            _parameters[key] = value;
            return this;
        }

        /// <summary>Adds form parameters to the token request.</summary>
        /// <param name="parameters">The parameters.</param>
        /// <returns>This builder.</returns>
        public OAuth2OptionsBuilder WithTokenRetrievalParameters(IReadOnlyDictionary<string, string> parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            foreach (var (key, value) in parameters)
            {
                _parameters[key] = value;
            }
            return this;
        }

        /// <summary>Uses a client key store for mTLS towards the target system.</summary>
        /// <param name="clientKeyStore">The key store (certificate with private key).</param>
        /// <returns>This builder.</returns>
        public OAuth2OptionsBuilder WithClientKeyStore(X509Certificate2Collection clientKeyStore)
        {
            ArgumentNullException.ThrowIfNull(clientKeyStore);
            _clientKeyStoreSupplier = () => clientKeyStore;
            return this;
        }

        internal OAuth2OptionsBuilder WithClientKeyStoreSupplier(Func<X509Certificate2Collection?> supplier)
        {
            _clientKeyStoreSupplier = supplier;
            return this;
        }

        /// <summary>Sets the token retrieval timeout.</summary>
        /// <param name="timeLimiter">The timeout.</param>
        /// <returns>This builder.</returns>
        public OAuth2OptionsBuilder WithTimeLimiter(ResilienceConfiguration.TimeLimiterConfiguration timeLimiter)
        {
            ArgumentNullException.ThrowIfNull(timeLimiter);
            _timeLimiter = timeLimiter;
            return this;
        }

        /// <summary>Sets the token cache configuration.</summary>
        /// <param name="tokenCacheParameters">The configuration.</param>
        /// <returns>This builder.</returns>
        public OAuth2OptionsBuilder WithTokenCacheParameters(TokenCacheParameters tokenCacheParameters)
        {
            ArgumentNullException.ThrowIfNull(tokenCacheParameters);
            _tokenCacheParameters = tokenCacheParameters;
            return this;
        }

        internal OAuth2OptionsBuilder WithBtpTenantApiBaseUri(Uri? btpTenantApiBaseUri)
        {
            _btpTenantApiBaseUri = btpTenantApiBaseUri;
            return this;
        }

        /// <summary>Creates the options.</summary>
        /// <returns>The options.</returns>
        public OAuth2Options Build()
        {
            if (_skipTokenRetrieval && _parameters.Count > 0)
            {
                Log.LogWarning(
                    "OAuth2Options are configured to skip the token retrieval, but there are also additional token request parameters. They are ignored.");
            }
            return new OAuth2Options(
                _skipTokenRetrieval,
                new Dictionary<string, string>(_parameters, StringComparer.Ordinal),
                _timeLimiter,
                _clientKeyStoreSupplier,
                _tokenCacheParameters,
                _btpTenantApiBaseUri);
        }
    }

    /// <summary>A service binding destination option that sets the token retrieval timeout.</summary>
    /// <param name="Value">The timeout.</param>
    public sealed record TokenRetrievalTimeout(ResilienceConfiguration.TimeLimiterConfiguration Value)
        : IOptionsEnhancer<ResilienceConfiguration.TimeLimiterConfiguration>;
}
