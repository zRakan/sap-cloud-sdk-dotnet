using System.Text;
using System.Text.RegularExpressions;
using Sap.Cloud.Sdk.CloudPlatform.RequestHeaders;
using Sap.Cloud.Sdk.CloudPlatform.Security.Exceptions;
using Sap.Cloud.Sdk.CloudPlatform.Threading;

namespace Sap.Cloud.Sdk.CloudPlatform.Security;

/// <summary>Provides the Basic Authentication credentials of the current request.</summary>
public interface IBasicAuthenticationFacade
{
    /// <summary>Gets the current credentials.</summary>
    /// <returns>The credentials, or a failure.</returns>
    Result<BasicCredentials> TryGetBasicCredentials();
}

/// <summary>Gives access to the Basic Authentication credentials of the current request.</summary>
public static class BasicAuthenticationAccessor
{
    private static IBasicAuthenticationFacade _facade = new DefaultBasicAuthenticationFacade();

    /// <summary>
    /// The facade providing the credentials. Setting <see langword="null"/> restores
    /// <see cref="DefaultBasicAuthenticationFacade"/>.
    /// </summary>
    public static IBasicAuthenticationFacade BasicAuthenticationFacade
    {
        get => _facade;
        set => _facade = value ?? new DefaultBasicAuthenticationFacade();
    }

    /// <summary>Gets the current credentials.</summary>
    /// <returns>The credentials, or a failure.</returns>
    public static Result<BasicCredentials> TryGetCurrentBasicCredentials() => _facade.TryGetBasicCredentials();
}

/// <summary>
/// Reads the credentials from the current <see cref="IThreadContext"/>, or decodes them from the <c>Authorization</c>
/// header of the current request.
/// </summary>
public partial class DefaultBasicAuthenticationFacade : IBasicAuthenticationFacade
{
    /// <inheritdoc/>
    public Result<BasicCredentials> TryGetBasicCredentials()
    {
        var context = ThreadContextAccessor.GetCurrentContextOrNull();
        if (context is not null && context.ContainsProperty(BasicAuthenticationThreadContextListener.PropertyBasicAuthHeader))
        {
            return context.GetPropertyValue<BasicCredentials>(
                BasicAuthenticationThreadContextListener.PropertyBasicAuthHeader);
        }
        return Result.Of(ExtractFromHeader);
    }

    private static BasicCredentials ExtractFromHeader()
    {
        var headers = RequestHeaderAccessor.TryGetHeaderContainer().GetValueOrThrow(e =>
            new BasicAuthenticationAccessException(
                "Unable to resolve basic credentials: Unable to read request headers.", e));

        var values = headers.GetHeaderValues("Authorization");
        if (values.Count == 0)
        {
            throw new BasicAuthenticationAccessException(
                "Unable to resolve basic credentials: No 'Authorization' header present in request.");
        }
        if (values.Count > 1)
        {
            throw new BasicAuthenticationAccessException(
                "Unable to resolve basic credentials: Received multiple 'Authorization' headers with the request, "
                + "but the specification allows at most one.");
        }

        var match = BasicPrefix().Match(values[0]);
        if (!match.Success)
        {
            throw new BasicAuthenticationAccessException(
                "Unable to resolve basic credentials: The 'Authorization' header did not contain a Basic "
                + "Authentication header field.");
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(match.Groups[1].Value));
        }
        catch (FormatException e)
        {
            throw new BasicAuthenticationAccessException("Unable to resolve basic credentials: Invalid format.", e);
        }

        // RFC 7617: the user-id must not contain a colon, the password may. Split at the first colon only.
        // (The Java SDK splits at every colon and thereby truncates such passwords.)
        var separator = decoded.IndexOf(':', StringComparison.Ordinal);
        if (separator < 0)
        {
            throw new BasicAuthenticationAccessException("Unable to resolve basic credentials: Invalid format.");
        }
        return new BasicCredentials(decoded[..separator], decoded[(separator + 1)..]);
    }

    [GeneratedRegex(@"^[Bb][Aa][Ss][Ii][Cc] \s*([A-Za-z0-9+/=]+)\s*$")]
    private static partial Regex BasicPrefix();
}

/// <summary>Stores the Basic Authentication credentials in the <see cref="IThreadContext"/>.</summary>
public class BasicAuthenticationThreadContextListener : IThreadContextListener
{
    /// <summary>The name of the thread context property holding the <see cref="BasicCredentials"/>.</summary>
    public const string PropertyBasicAuthHeader =
        "Sap.Cloud.Sdk.CloudPlatform.Security.BasicAuthenticationThreadContextListener:basicAuthHeader";

    /// <inheritdoc/>
    public int Priority => ThreadContextListenerPriorities.BasicAuthListener;

    /// <inheritdoc/>
    public void AfterInitialize(IThreadContext threadContext)
    {
        ArgumentNullException.ThrowIfNull(threadContext);
        threadContext.SetPropertyIfAbsent(
            PropertyBasicAuthHeader,
            () => ThreadContextProperty.OfConfidentialResult(BasicAuthenticationAccessor.TryGetCurrentBasicCredentials()));
    }
}
