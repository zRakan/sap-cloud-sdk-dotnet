using System.Text;

namespace Sap.Cloud.Sdk.CloudPlatform.Security;

/// <summary>Marker interface for credentials used to authenticate outbound requests.</summary>
/// <remarks>
/// Named <c>Credentials</c> in the Java SDK; renamed to avoid a clash with <see cref="System.Net.ICredentials"/>.
/// </remarks>
#pragma warning disable CA1040 // Marker interface kept from the Java SDK; connectivity code switches on its implementations.
public interface ISecurityCredentials
#pragma warning restore CA1040
{
}

/// <summary>Indicates that no credentials are used.</summary>
public sealed record NoCredentials : ISecurityCredentials;

/// <summary>Username and password for HTTP Basic Authentication.</summary>
public sealed record BasicCredentials : ISecurityCredentials
{
    /// <summary>Creates credentials.</summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    public BasicCredentials(string username, string password)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);
        Username = username;
        Password = password;
    }

    /// <summary>The username.</summary>
    public string Username { get; }

    /// <summary>The password.</summary>
    public string Password { get; }

    /// <summary>The value for the HTTP <c>Authorization</c> header: <c>Basic &lt;base64(username:password)&gt;</c>.</summary>
    public string HttpHeaderValue => "Basic " + BasicAuthHeaderEncoder.EncodeUserPasswordBase64(Username, Password);

    /// <summary>Hides the password.</summary>
    /// <returns>A string representation.</returns>
    public override string ToString() => $"BasicCredentials(username={Username}, password=(hidden))";
}

/// <summary>A bearer token.</summary>
public sealed record BearerCredentials : ISecurityCredentials
{
    private const string BearerPrefix = "Bearer ";

    /// <summary>Creates credentials. A leading <c>Bearer </c> prefix (any casing) is removed.</summary>
    /// <param name="token">The token, with or without prefix.</param>
    public BearerCredentials(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var trimmed = token.Trim();
        Token = trimmed.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed[BearerPrefix.Length..].Trim()
            : trimmed;
    }

    /// <summary>The token, without prefix.</summary>
    public string Token { get; }

    /// <summary>The value for the HTTP <c>Authorization</c> header: <c>Bearer &lt;token&gt;</c>.</summary>
    public string HttpHeaderValue => BearerPrefix + Token;

    /// <summary>Hides the token.</summary>
    /// <returns>A string representation.</returns>
    public override string ToString() => "BearerCredentials(token=(hidden))";
}

/// <summary>OAuth 2.0 client ID and secret.</summary>
/// <param name="ClientId">The client ID.</param>
/// <param name="ClientSecret">The client secret.</param>
public sealed record ClientCredentials(string ClientId, string ClientSecret) : ISecurityCredentials
{
    /// <summary>Hides the secret.</summary>
    /// <returns>A string representation.</returns>
    public override string ToString() => $"ClientCredentials(clientId={ClientId}, clientSecret=(hidden))";
}

/// <summary>OAuth 2.0 client ID with an X.509 client certificate and private key (PEM).</summary>
/// <param name="ClientId">The client ID.</param>
/// <param name="Certificate">The certificate (PEM).</param>
/// <param name="Key">The private key (PEM).</param>
public sealed record ClientCertificate(string ClientId, string Certificate, string Key) : ISecurityCredentials
{
    /// <summary>Hides the certificate and key.</summary>
    /// <returns>A string representation.</returns>
    public override string ToString() => $"ClientCertificate(clientId={ClientId}, certificate=(hidden), key=(hidden))";
}

/// <summary>Encodes credentials for HTTP Basic Authentication.</summary>
public static class BasicAuthHeaderEncoder
{
    /// <summary>Encodes <c>username:password</c> as Base64 (UTF-8).</summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <returns>The encoded value, without the <c>Basic </c> prefix.</returns>
    public static string EncodeUserPasswordBase64(string username, string password) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));

    /// <summary>Encodes Basic credentials as Base64.</summary>
    /// <param name="credentials">The credentials.</param>
    /// <returns>The encoded value, without the <c>Basic </c> prefix.</returns>
    public static string EncodeUserPasswordBase64(BasicCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        return EncodeUserPasswordBase64(credentials.Username, credentials.Password);
    }

    /// <summary>Encodes OAuth client credentials as Base64.</summary>
    /// <param name="credentials">The credentials.</param>
    /// <returns>The encoded value, without the <c>Basic </c> prefix.</returns>
    public static string EncodeClientCredentialsBase64(ClientCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        return EncodeUserPasswordBase64(credentials.ClientId, credentials.ClientSecret);
    }
}
