using System.Globalization;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// Builds a destination from the JSON response of the destination service: a <see cref="DefaultHttpDestination"/> for
/// type <c>HTTP</c>, a <see cref="DefaultDestination"/> for everything else (RFC destinations are not ported).
/// </summary>
internal static class DestinationServiceFactory
{
    private const string ProviderTenantId = ""; // as per contract in DestinationProperty.TenantId

    internal static IDestination FromDestinationServiceV1Response(DestinationServiceV1Response response) =>
        FromDestinationServiceV1Response(response, OnBehalfOf.NamedUserCurrentTenant);

    /// <exception cref="DestinationAccessException">An authentication token carries an error, or the key material cannot be read.</exception>
    internal static IDestination FromDestinationServiceV1Response(DestinationServiceV1Response response, OnBehalfOf onBehalfOf)
    {
        ArgumentNullException.ThrowIfNull(response);
        var configuration = response.DestinationConfiguration;
        var baseBuilder = DefaultDestination.FromMap(configuration.ToDictionary(p => p.Key, p => (object)p.Value));

        // enable change detection
        baseBuilder.Property(DestinationProperty.PropertiesForChangeDetection, (IReadOnlyCollection<string>)[.. configuration.Keys]);

        // enable tenant id
        baseBuilder.Property(DestinationProperty.TenantId, GetDestinationTenantId(onBehalfOf));

        // store auth tokens, if any
        StoreAuthTokens(baseBuilder, response.AuthTokens);

        // finalize HTTP destination
        if (TryGetType(baseBuilder) == DestinationType.Http)
        {
            return HandleHttpDestination(baseBuilder.Build(), response.Certificates, response.AuthTokens);
        }

        // RFC destinations are not ported (guide section 9): they stay plain destinations, like all other types.
        return baseBuilder.Build();
    }

    private static DestinationType? TryGetType(DefaultDestination.Builder builder) =>
        builder.TryGet(DestinationProperty.Type, out var type) ? type : null;

    private static string GetDestinationTenantId(OnBehalfOf onBehalfOf) => onBehalfOf switch
    {
        // TechnicalUserProvider <- AlwaysProvider
        OnBehalfOf.TechnicalUserProvider => ProviderTenantId,
        OnBehalfOf.NamedUserCurrentTenant or OnBehalfOf.TechnicalUserCurrentTenant =>
            TenantAccessor.TryGetCurrentTenant().Map(t => t.TenantId).GetValueOrDefault() ?? ProviderTenantId,
        _ => throw new InvalidOperationException("Unknown OnBehalfOf: " + onBehalfOf),
    };

    private static DefaultHttpDestination HandleHttpDestination(
        DefaultDestination baseProperties,
        IReadOnlyList<DestinationServiceV1Response.DestinationCertificate>? certificates,
        IReadOnlyList<DestinationServiceV1Response.DestinationAuthToken>? authTokens)
    {
        var builder = DefaultHttpDestination.FromProperties(baseProperties);

        // enable certificates and truststore/keystore
        if (certificates is { Count: > 0 })
        {
            foreach (var certificate in certificates)
            {
                DetermineAndSetCertificateExpirationTime(certificate);
            }
            builder.Property(DestinationProperty.Certificates, (IReadOnlyList<object?>)[.. certificates]);

            var extractor = new DestinationKeyStoreExtractor(new BuilderProperties(builder));
            if (extractor.GetKeyStore() is { } keyStore)
            {
                builder.KeyStore(keyStore);
            }
            if (extractor.GetTrustStore() is { } trustStore)
            {
                builder.TrustStore(trustStore);
            }
        }

        // enable auth tokens
        if (authTokens is { Count: > 0 })
        {
            builder.HeaderProviders(new AuthTokenHeaderProvider());
        }

        return builder.Build();
    }

    private static void ThrowOnTokenError(string? destinationName, DestinationServiceV1Response.DestinationAuthToken token)
    {
        if (token.Error is not null)
        {
            throw new DestinationAccessException(
                $"Failed to read authentication token of destination '{destinationName}'. "
                + $"The destination service responded with an error: '{token.Error}'.\n"
                + "In case only the properties of a destination should be accessed, without performing authorization flows, "
                + "please use the 'GetDestinationPropertiesAsync' method on 'DestinationService' instead.");
        }
    }

    private static void StoreAuthTokens(
        DefaultDestination.Builder builder,
        IReadOnlyList<DestinationServiceV1Response.DestinationAuthToken>? authTokens)
    {
        if (authTokens is not { Count: > 0 })
        {
            return;
        }

        var authType = AuthenticationType.NoAuthentication;
        try
        {
            if (builder.TryGet(DestinationProperty.AuthType, out var parsed))
            {
                authType = parsed;
            }
        }
        catch (ArgumentException)
        {
            // an unknown authentication type is treated like a missing one
        }
        builder.TryGet(DestinationProperty.Name, out var name);
        foreach (var token in authTokens)
        {
            ThrowOnTokenError(name, token);
        }
        foreach (var token in authTokens)
        {
            SetExpirationTimestamp(token, authType);
        }

        // Note: it is important that the auth tokens are added as property here
        // for the HttpClient cache we need to include them in the cache key
        // since they may contain cookies for which we need to make sure they
        // are not shared between different http clients
        builder.Property(DestinationProperty.AuthTokens, (IReadOnlyList<object?>)[.. authTokens]);
    }

    private static void SetExpirationTimestamp(DestinationServiceV1Response.DestinationAuthToken authToken, AuthenticationType authType)
    {
        long? expiresIn = null;
        if (!string.IsNullOrWhiteSpace(authToken.ExpiresIn))
        {
            if (!long.TryParse(authToken.ExpiresIn, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            {
                throw new DestinationAccessException(
                    "The destination service returned an authentication token with an invalid 'expires_in' value.");
            }
            expiresIn = seconds;
        }
        // any auth token other than basic auth headers without expiration date are assumed to expire after default duration
        if (expiresIn is null && authType != AuthenticationType.BasicAuthentication)
        {
            expiresIn = (long?)DestinationService.Cache.ExpirationDuration?.TotalSeconds;
        }
        if (expiresIn is { } value)
        {
            authToken.ExpiryTimestamp = DateTimeOffset.UtcNow.AddSeconds(value);
        }
    }

    private static void DetermineAndSetCertificateExpirationTime(DestinationServiceV1Response.DestinationCertificate certificate)
    {
        if (!DestinationService.Cache.IsEnabled || !DestinationService.Cache.IsChangeDetectionEnabled)
        {
            // currently parsing certificates and determining the actual expiration time is not yet implemented
            return;
        }

        // Changes to a certificate in the BTP may not be detected by the caching change detection feature.
        // Specifically, change detection only works if the certificate name changes.
        // To be "up to date" (according to the change detection interval configured), we write this time to the destination.
        // This date is picked up by the change detection, if enabled. If not enabled, this date is disregarded.
        var duration = DestinationService.Cache.ExpirationDuration ?? DestinationService.Cache.DefaultExpirationDuration;
        certificate.ExpiryTimestamp = DateTimeOffset.UtcNow.Add(duration);
    }

    private sealed class BuilderProperties(DefaultHttpDestination.Builder builder) : IPropertyKeyExtractor
    {
        public bool TryGet<T>(DestinationPropertyKey<T> key, out T value)
        {
            var found = builder.TryGet(key, out var result);
            value = result!;
            return found;
        }
    }
}
