using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// Adds the <c>sap-client</c> and <c>sap-language</c> headers of SAP ERP destinations.
/// </summary>
/// <remarks>
/// <c>sap-language</c> is taken from <see cref="CultureInfo.CurrentCulture"/> if the destination sets
/// <c>cloudsdk.dynamicSapLanguage</c> to <c>true</c> (replacing Java's <c>LocaleAccessor</c>), otherwise from the
/// <c>sap-language</c> property. Only the language part of a culture or language tag is sent. An empty language
/// (for example the invariant culture) produces no header.
/// </remarks>
public sealed class ErpDestinationHeaderProvider : IDestinationHeaderProvider
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<ErpDestinationHeaderProvider>();

    /// <inheritdoc/>
    public IReadOnlyList<Header> GetHeaders(DestinationRequestContext requestContext)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        var result = new List<Header>();
        IDestinationProperties destination = requestContext.Destination;

        if (destination.TryGet(DestinationProperty.SapClient, out var client))
        {
            result.Add(new Header(DestinationProperty.SapClient.KeyName, client));
        }
        else
        {
            WarnIfDestinationIsOnPremise(destination);
        }

        string? language = null;
        if (destination.TryGet(DestinationProperty.DynamicSapLanguage, out var dynamic) && dynamic)
        {
            language = LanguageOf(CultureInfo.CurrentCulture.Name);
        }
        else if (destination.TryGet(DestinationProperty.SapLanguage, out var tag))
        {
            language = LanguageOf(tag);
        }
        if (!string.IsNullOrEmpty(language))
        {
            result.Add(new Header(DestinationProperty.SapLanguage.KeyName, language));
        }
        return result;
    }

    private static string LanguageOf(string languageTag)
    {
        var end = languageTag.IndexOfAny(['-', '_']);
        return (end < 0 ? languageTag : languageTag[..end]).ToLowerInvariant();
    }

    private static void WarnIfDestinationIsOnPremise(IDestinationProperties destination)
    {
        if (destination.TryGet(DestinationProperty.ProxyType, out var proxyType) && proxyType == ProxyType.OnPremise)
        {
            if (Log.IsEnabled(LogLevel.Information))
            {
                Log.LogInformation(
                    "No {Property} property defined on HTTP destination pointing to on-premise ERP system with URI {Uri}. "
                    + "It is recommended to specify that property to prevent authentication issues.",
                    DestinationProperty.SapClient.KeyName,
                    destination.GetOrDefault(DestinationProperty.Uri, string.Empty));
            }
        }
    }
}
