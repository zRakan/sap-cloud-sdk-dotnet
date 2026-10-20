using Sap.Cloud.Sdk.CloudPlatform.Tenant;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Adds an OAuth 2.0 bearer token to every request of a destination.</summary>
internal sealed class OAuth2HeaderProvider(OAuth2Service oauth2Service, string authHeaderName) : IDestinationHeaderProvider
{
    internal OAuth2Service Service => oauth2Service;

    internal string AuthHeaderName => authHeaderName;

    /// <summary>
    /// Blocks on <see cref="GetHeadersAsync"/>. Kept for parity with the synchronous Java API; the SDK's HTTP pipeline
    /// always uses the asynchronous variant.
    /// </summary>
    public IReadOnlyList<Header> GetHeaders(DestinationRequestContext requestContext) =>
        GetHeadersAsync(requestContext, CancellationToken.None).AsTask().GetAwaiter().GetResult();

    public async ValueTask<IReadOnlyList<Header>> GetHeadersAsync(
        DestinationRequestContext requestContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        AssertTenantRemainedConsistent(requestContext.Destination);
        var accessToken = await oauth2Service.RetrieveAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        return [new Header(authHeaderName, "Bearer " + accessToken)];
    }

    private static void AssertTenantRemainedConsistent(IDestinationProperties destination)
    {
        if (!destination.TryGet(DestinationProperty.TenantId, out var destinationTenantId))
        {
            return;
        }
        var currentTenantId = TenantAccessor.TryGetCurrentTenant().Map(t => t.TenantId).GetValueOrDefault() ?? "";
        if (!string.Equals(destinationTenantId, currentTenantId, StringComparison.Ordinal))
        {
            destination.TryGet(DestinationProperty.Name, out var name);
            throw new InvalidOperationException(
                $"Tenant ID of destination '{name}' does not match the current tenant ID. Destination was created specifically "
                + $"for tenant '{destinationTenantId}', but the current tenant is '{currentTenantId}'.");
        }
    }
}
