using System.Globalization;
using System.Net;
using System.Text.Json;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Looks up the IAS subdomain of a tenant via the BTP tenant API.</summary>
internal class IasTenantHostResolver(HttpClient httpClient)
{
    internal static readonly IasTenantHostResolver Default = new(new HttpClient());

    public virtual async Task<string> ResolveAsync(Uri btpTenantApiUri, string tenantId, CancellationToken cancellationToken)
    {
        var url = new Uri(btpTenantApiUri, string.Create(CultureInfo.InvariantCulture, $"/sap/rest/tenantLoginInfo?id={Uri.EscapeDataString(tenantId)}"));
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException e)
        {
            throw new DestinationAccessException("Failed to query BTP tenant API: " + e.Message, e);
        }
        using (response)
        {
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new DestinationAccessException(
                    $"Failed to query BTP tenant API: Server returned status code {(int)response.StatusCode} for GET request to '{url}'.");
            }
            return ExtractSubdomainFromTokenEndpoint(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        }
    }

    internal static string ExtractSubdomainFromTokenEndpoint(string responseBody)
    {
        try
        {
            using var json = JsonDocument.Parse(responseBody);
            var host = new Uri(json.RootElement.GetProperty("token_endpoint").GetString()!).Host;
            return host[..host.IndexOf('.', StringComparison.Ordinal)];
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or UriFormatException or ArgumentException)
        {
            throw new DestinationAccessException(
                "Failed to extract IAS tenant host from the BTP tenant API response. The response did not conform to the expected format: " + responseBody,
                e);
        }
    }
}
