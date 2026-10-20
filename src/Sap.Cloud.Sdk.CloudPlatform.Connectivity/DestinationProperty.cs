using System.Globalization;
using static Sap.Cloud.Sdk.CloudPlatform.Connectivity.DestinationPropertyKey;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>The well-known properties of a destination.</summary>
/// <remarks>
/// Java's <c>SCREAMING_CASE</c> constants are PascalCase here. The members that are package-private in Java
/// (<see cref="PropertiesForChangeDetection"/>, <see cref="TenantId"/>, <see cref="SecurityConfiguration"/>) are public
/// because the modules that use them live in separate assemblies.
/// </remarks>
public static class DestinationProperty
{
    /// <summary>The destination name (<c>Name</c>).</summary>
    public static readonly DestinationPropertyKey<string> Name = CreateStringProperty("Name");

    /// <summary>The destination type (<c>Type</c>).</summary>
    public static readonly DestinationPropertyKey<DestinationType> Type =
        CreateProperty("Type", DestinationTypeExtensions.OfIdentifier);

    /// <summary>The destination URL (<c>URL</c>).</summary>
    public static readonly DestinationPropertyKey<string> Uri = CreateStringProperty("URL");

    /// <summary>The authentication type (<c>Authentication</c>).</summary>
    public static readonly DestinationPropertyKey<AuthenticationType> AuthType =
        CreateProperty("Authentication", AuthenticationTypeExtensions.OfIdentifier);

    /// <summary>The fallback for <see cref="AuthType"/> (<c>authType</c>).</summary>
    public static readonly DestinationPropertyKey<AuthenticationType> AuthTypeFallback =
        CreateProperty("authType", AuthenticationTypeExtensions.OfIdentifier);

    /// <summary>The Basic authentication user (<c>User</c>).</summary>
    public static readonly DestinationPropertyKey<string> BasicAuthUsername = CreateStringProperty("User");

    /// <summary>The fallback for <see cref="BasicAuthUsername"/> (<c>username</c>).</summary>
    public static readonly DestinationPropertyKey<string> BasicAuthUsernameFallback = CreateStringProperty("username");

    /// <summary>The Basic authentication password (<c>Password</c>).</summary>
    public static readonly DestinationPropertyKey<string> BasicAuthPassword = CreateStringProperty("Password");

    /// <summary>The TLS version (<c>TLSVersion</c>).</summary>
    public static readonly DestinationPropertyKey<string> TlsVersion = CreateStringProperty("TLSVersion");

    /// <summary>The proxy authorization header value (<c>ProxyAuthorization</c>).</summary>
    public static readonly DestinationPropertyKey<string> ProxyAuth = CreateStringProperty("ProxyAuthorization");

    /// <summary>The proxy URI (<c>Proxy</c>).</summary>
    public static readonly DestinationPropertyKey<Uri> ProxyUri = CreateUriProperty("Proxy");

    /// <summary>The proxy host (<c>ProxyHost</c>).</summary>
    public static readonly DestinationPropertyKey<string> ProxyHost = CreateStringProperty("ProxyHost");

    /// <summary>The proxy port (<c>ProxyPort</c>).</summary>
    public static readonly DestinationPropertyKey<int> ProxyPort =
        CreateProperty("ProxyPort", s => int.Parse(s, CultureInfo.InvariantCulture));

    /// <summary>The proxy type (<c>ProxyType</c>).</summary>
    public static readonly DestinationPropertyKey<ProxyType> ProxyType =
        CreateProperty("ProxyType", ProxyTypeExtensions.OfIdentifierSensitive);

    /// <summary>The key store location (<c>KeyStoreLocation</c>).</summary>
    public static readonly DestinationPropertyKey<string> KeyStoreLocation = CreateStringProperty("KeyStoreLocation");

    /// <summary>The key store password (<c>KeyStorePassword</c>).</summary>
    public static readonly DestinationPropertyKey<string> KeyStorePassword = CreateStringProperty("KeyStorePassword");

    /// <summary>The trust store location (<c>TrustStoreLocation</c>).</summary>
    public static readonly DestinationPropertyKey<string> TrustStoreLocation = CreateStringProperty("TrustStoreLocation");

    /// <summary>The trust store password (<c>TrustStorePassword</c>).</summary>
    public static readonly DestinationPropertyKey<string> TrustStorePassword = CreateStringProperty("TrustStorePassword");

    /// <summary>Whether all certificates are trusted (<c>TrustAll</c>).</summary>
    public static readonly DestinationPropertyKey<bool> TrustAll = CreateProperty("TrustAll", ParseBoolean);

    /// <summary>The fallback for <see cref="TrustAll"/> (<c>isTrustingAllCertificates</c>).</summary>
    public static readonly DestinationPropertyKey<bool> TrustAllFallback =
        CreateProperty("isTrustingAllCertificates", ParseBoolean);

    /// <summary>The principal propagation mode (<c>cloudsdk.principalPropagationMode</c>).</summary>
    public static readonly DestinationPropertyKey<PrincipalPropagationMode> PrincipalPropagationMode =
        CreateProperty("cloudsdk.principalPropagationMode", PrincipalPropagationModeExtensions.OfIdentifier);

    /// <summary>The certificates of the destination (<c>cloudsdk.certificates</c>).</summary>
    public static readonly DestinationPropertyKey<IReadOnlyList<object?>> Certificates =
        CreateListProperty("cloudsdk.certificates");

    /// <summary>The authentication tokens of the destination (<c>cloudsdk.authTokens</c>).</summary>
    public static readonly DestinationPropertyKey<IReadOnlyList<object?>> AuthTokens =
        CreateListProperty("cloudsdk.authTokens");

    /// <summary>The Cloud Connector location ID (<c>CloudConnectorLocationId</c>).</summary>
    public static readonly DestinationPropertyKey<string> CloudConnectorLocationId =
        CreateStringProperty("CloudConnectorLocationId");

    /// <summary>Whether the authentication token is forwarded (<c>forwardAuthToken</c>).</summary>
    public static readonly DestinationPropertyKey<bool> ForwardAuthToken =
        CreateProperty("forwardAuthToken", ParseBoolean);

    /// <summary>Whether the authentication token is forwarded, as set by the app router (<c>HTML5.ForwardAuthToken</c>).</summary>
    public static readonly DestinationPropertyKey<bool> ApprouterForwardAuthToken =
        CreateProperty("HTML5.ForwardAuthToken", ParseBoolean);

    /// <summary>The system user (<c>SystemUser</c>).</summary>
    public static readonly DestinationPropertyKey<string> SystemUser = CreateStringProperty("SystemUser");

    /// <summary>The SAP client (<c>sap-client</c>).</summary>
    public static readonly DestinationPropertyKey<string> SapClient = CreateStringProperty("sap-client");

    /// <summary>The SAP language (<c>sap-language</c>).</summary>
    public static readonly DestinationPropertyKey<string> SapLanguage = CreateStringProperty("sap-language");

    /// <summary>Whether the SAP language follows the current culture (<c>cloudsdk.dynamicSapLanguage</c>).</summary>
    public static readonly DestinationPropertyKey<bool> DynamicSapLanguage =
        CreateProperty("cloudsdk.dynamicSapLanguage", ParseBoolean);

    /// <summary>The names of the properties relevant for change detection (<c>cloudsdk.propertiesForChangeDetection</c>).</summary>
    public static readonly DestinationPropertyKey<IReadOnlyCollection<string>> PropertiesForChangeDetection =
        CreateCollectionProperty("cloudsdk.propertiesForChangeDetection");

    /// <summary>The tenant the destination belongs to (<c>cloudsdk.tenantId</c>).</summary>
    public static readonly DestinationPropertyKey<string> TenantId = CreateStringProperty("cloudsdk.tenantId");

    /// <summary>The security configuration strategy (<c>cloudsdk.securityConfiguration</c>).</summary>
    public static readonly DestinationPropertyKey<SecurityConfigurationStrategy> SecurityConfiguration =
        CreateProperty("cloudsdk.securityConfiguration", SecurityConfigurationStrategyExtensions.OfIdentifierOrDefault);

    // Java's Boolean.valueOf(String): true if and only if the string equals "true", ignoring case.
    private static bool ParseBoolean(string value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
