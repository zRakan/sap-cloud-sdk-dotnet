using System.Globalization;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Security;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Reads the OAuth 2.0 properties of a service binding.</summary>
public interface IOAuth2PropertySupplier
{
    /// <summary>Whether the binding contains OAuth 2.0 credentials this supplier can handle.</summary>
    bool IsOAuth2Binding { get; }

    /// <summary>The URL of the service.</summary>
    Uri ServiceUri { get; }

    /// <summary>The token endpoint.</summary>
    Uri TokenUri { get; }

    /// <summary>The client identity: <see cref="ClientCredentials"/> or <see cref="ClientCertificate"/>.</summary>
    ISecurityCredentials ClientIdentity { get; }

    /// <summary>Further token retrieval options.</summary>
    OAuth2Options OAuth2Options => OAuth2Options.Default;
}

/// <summary>The credential type of an XSUAA or IAS binding (<c>credential-type</c>).</summary>
public enum CredentialType
{
    /// <summary><c>binding-secret</c>.</summary>
    BindingSecret,

    /// <summary><c>instance-secret</c>.</summary>
    InstanceSecret,

    /// <summary><c>x509</c>.</summary>
    X509,

    /// <summary><c>X509_GENERATED</c>.</summary>
    X509Generated,

    /// <summary><c>X509_PROVIDED</c> (not supported).</summary>
    X509Provided,

    /// <summary><c>X509_ATTESTED</c> (zero-trust identity service; not supported).</summary>
    X509Attested,
}

/// <summary>
/// Reads OAuth 2.0 properties from the credentials of a service binding: <c>clientid</c>, <c>clientsecret</c> or
/// <c>certificate</c>/<c>key</c>, <c>url</c>/<c>certurl</c> below an OAuth property path (by default <c>uaa</c>).
/// </summary>
public class DefaultOAuth2PropertySupplier : IOAuth2PropertySupplier
{
    /// <summary>The default path of the OAuth credentials within the binding credentials.</summary>
    public static readonly IReadOnlyList<string> DefaultUaaCredentialPath = ["uaa"];

    private static readonly ILogger Log = SdkLogging.CreateLogger<DefaultOAuth2PropertySupplier>();

    private readonly IReadOnlyList<string> _oauthPropertyPath;

    /// <summary>Creates a supplier that reads the OAuth credentials below <c>uaa</c>.</summary>
    /// <param name="options">The binding options.</param>
    public DefaultOAuth2PropertySupplier(ServiceBindingDestinationOptions options)
        : this(options, DefaultUaaCredentialPath)
    {
    }

    /// <summary>Creates a supplier that reads the OAuth credentials below <paramref name="oauthPropertyPath"/>.</summary>
    /// <param name="options">The binding options.</param>
    /// <param name="oauthPropertyPath">The path; empty for the credentials root.</param>
    public DefaultOAuth2PropertySupplier(ServiceBindingDestinationOptions options, IReadOnlyList<string> oauthPropertyPath)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(oauthPropertyPath);
        Options = options;
        _oauthPropertyPath = oauthPropertyPath;
    }

    /// <summary>The binding options.</summary>
    protected ServiceBindingDestinationOptions Options { get; }

    /// <inheritdoc/>
    public virtual bool IsOAuth2Binding => GetOAuthCredential<string>("clientid") is not null;

    /// <inheritdoc/>
    public virtual Uri ServiceUri => GetCredentialOrThrow<Uri>("url");

    /// <inheritdoc/>
    public virtual Uri TokenUri => GetOAuthCredentialOrThrow<Uri>(GetCredentialType() switch
    {
        CredentialType.X509 or CredentialType.X509Generated or CredentialType.X509Provided or CredentialType.X509Attested => "certurl",
        _ => "url",
    });

    /// <inheritdoc/>
    public virtual ISecurityCredentials ClientIdentity
    {
        get
        {
            var clientId = GetOAuthCredentialOrThrow<string>("clientid");
            return GetCredentialType() switch
            {
                CredentialType.BindingSecret or CredentialType.InstanceSecret =>
                    new ClientCredentials(clientId, GetOAuthCredentialOrThrow<string>("clientsecret")),
                CredentialType.X509 or CredentialType.X509Generated =>
                    new ClientCertificate(clientId, GetOAuthCredentialOrThrow<string>("certificate"), GetOAuthCredentialOrThrow<string>("key")),
                CredentialType.X509Attested => throw new DestinationAccessException(
                    "Credential type X509_ATTESTED (zero-trust identity service) is not supported by the SAP Cloud SDK for .NET."),
                _ => throw new DestinationAccessException(
                    "Credential type X509_PROVIDED is not supported. Please use X509_GENERATED instead."),
            };
        }
    }

    /// <inheritdoc/>
    public virtual OAuth2Options OAuth2Options
    {
        get
        {
            var builder = OAuth2Options.Builder();
            if (Options.TryGetOption<OAuth2Options.TokenRetrievalTimeout, Resilience.ResilienceConfiguration.TimeLimiterConfiguration>(out var timeout))
            {
                builder.WithTimeLimiter(timeout);
            }
            if (Options.TryGetOption<OAuth2.TokenCacheParameters, OAuth2.TokenCacheParameters>(out var cache))
            {
                builder.WithTokenCacheParameters(cache);
            }
            return builder.Build();
        }
    }

    /// <summary>The <c>credential-type</c> of the OAuth credentials; <see cref="CredentialType.BindingSecret"/> if absent.</summary>
    /// <returns>The credential type.</returns>
    protected internal CredentialType GetCredentialType()
    {
        var value = GetOAuthCredential<string>("credential-type");
        var type = ParseCredentialType(value);
        if (type is null)
        {
            Log.LogWarning("Credential type not found or not recognised in service binding. Defaulting to BINDING_SECRET.");
        }
        return type ?? CredentialType.BindingSecret;
    }

    internal static CredentialType? ParseCredentialType(string? value) => value?.ToUpperInvariant() switch
    {
        "X509" => CredentialType.X509,
        "INSTANCE-SECRET" => CredentialType.InstanceSecret,
        "BINDING-SECRET" => CredentialType.BindingSecret,
        "X509_GENERATED" => CredentialType.X509Generated,
        "X509_PROVIDED" => CredentialType.X509Provided,
        "X509_ATTESTED" => CredentialType.X509Attested,
        _ => null,
    };

    /// <summary>Reads a value at <paramref name="path"/> from the credentials root, or throws.</summary>
    /// <typeparam name="T"><see cref="string"/> or <see cref="Uri"/>.</typeparam>
    /// <param name="path">The property path.</param>
    /// <returns>The value.</returns>
    /// <exception cref="DestinationAccessException">The value is missing or cannot be converted.</exception>
    protected T GetCredentialOrThrow<T>(params string[] path)
        where T : class =>
        GetCredential<T>(path) ?? throw new DestinationAccessException(
            $"Failed to resolve property [{string.Join(", ", path)}] from service binding.");

    /// <summary>Reads a value at <paramref name="path"/> from the credentials root.</summary>
    /// <typeparam name="T"><see cref="string"/> or <see cref="Uri"/>.</typeparam>
    /// <param name="path">The property path.</param>
    /// <returns>The value, or <see langword="null"/> if missing.</returns>
    /// <exception cref="DestinationAccessException">The value cannot be converted.</exception>
    protected T? GetCredential<T>(params string[] path)
        where T : class => (T?)Convert<T>(Find(path));

    /// <summary>Reads an integer at <paramref name="path"/> from the credentials root.</summary>
    /// <param name="path">The property path.</param>
    /// <returns>The value, or <see langword="null"/> if missing.</returns>
    /// <exception cref="DestinationAccessException">The value is not an integer.</exception>
    protected int? GetIntCredential(params string[] path) => (int?)Convert<int>(Find(path));

    /// <summary>Reads a value below the OAuth property path, or throws.</summary>
    /// <typeparam name="T"><see cref="string"/> or <see cref="Uri"/>.</typeparam>
    /// <param name="path">The property path below the OAuth path.</param>
    /// <returns>The value.</returns>
    /// <exception cref="DestinationAccessException">The value is missing or cannot be converted.</exception>
    protected T GetOAuthCredentialOrThrow<T>(params string[] path)
        where T : class =>
        GetOAuthCredential<T>(path) ?? throw new DestinationAccessException(
            $"Failed to resolve property [{string.Join(", ", _oauthPropertyPath.Concat(path))}] from service binding.");

    /// <summary>Reads a value below the OAuth property path.</summary>
    /// <typeparam name="T"><see cref="string"/> or <see cref="Uri"/>.</typeparam>
    /// <param name="path">The property path below the OAuth path.</param>
    /// <returns>The value, or <see langword="null"/> if missing.</returns>
    protected T? GetOAuthCredential<T>(params string[] path)
        where T : class => (T?)Convert<T>(Find([.. _oauthPropertyPath, .. path]));

    private object? Find(IReadOnlyList<string> path)
    {
        object? node = Options.ServiceBinding.Credentials;
        foreach (var name in path)
        {
            if (node is not IReadOnlyDictionary<string, object?> map || !map.TryGetValue(name, out node))
            {
                return null;
            }
        }
        return node;
    }

    internal static object? Convert<T>(object? value)
    {
        if (value is null)
        {
            return null;
        }
        if (value is T)
        {
            return value;
        }
        if (typeof(T) == typeof(string) && value is string or long or double or bool)
        {
            return System.Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        if (typeof(T) == typeof(Uri))
        {
            return Uri.TryCreate(value.ToString(), UriKind.Absolute, out var uri)
                ? uri
                : throw new DestinationAccessException($"Unable to convert '{value}' into an URI.");
        }
        if (typeof(T) == typeof(int))
        {
            return value switch
            {
                long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
                string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) => i,
                _ => throw new DestinationAccessException($"Unable to convert '{value}' into an Integer."),
            };
        }
        throw new DestinationAccessException(
            $"Property value of type {value.GetType().Name} could not be parsed into type {typeof(T).Name}.");
    }

    /// <inheritdoc/>
    public override string ToString() => $"{GetType().Name}({Options.ServiceBinding.GetServiceIdentifier()})";
}
