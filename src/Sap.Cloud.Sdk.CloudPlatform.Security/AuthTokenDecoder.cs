using Microsoft.IdentityModel.JsonWebTokens;
using Sap.Cloud.Sdk.CloudPlatform.RequestHeaders;
using Sap.Cloud.Sdk.CloudPlatform.Security.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Security;

/// <summary>Decodes (without validating) JWT bearer tokens.</summary>
internal interface IAuthTokenDecoder
{
    /// <summary>Decodes an encoded JWT.</summary>
    /// <param name="encodedJwt">The encoded JWT.</param>
    /// <returns>The token.</returns>
    /// <exception cref="AuthTokenAccessException">If the value is not a JWT.</exception>
    AuthToken Decode(string encodedJwt);

    /// <summary>Decodes the bearer token from the <c>Authorization</c> header.</summary>
    /// <param name="headers">The request headers.</param>
    /// <returns>The token, or a failure.</returns>
    Result<AuthToken> Decode(IRequestHeaderContainer headers)
    {
        var values = headers.GetHeaderValues("Authorization");
        if (values.Count == 0)
        {
            return Result.Failure<AuthToken>(new AuthTokenAccessException(
                "Failed to decode JWT bearer: no 'Authorization' header present in request."));
        }
        if (values.Count > 1)
        {
            return Result.Failure<AuthToken>(new AuthTokenAccessException(
                "Failed to decode JWT bearer: multiple 'Authorization' headers present in request."));
        }

        const string bearerPrefix = "bearer ";
        var value = values[0];
        if (!value.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<AuthToken>(new AuthTokenAccessException(
                "Failed to decode JWT bearer: no JWT bearer present in 'Authorization' header of request."));
        }
        var token = value[bearerPrefix.Length..];
        return Result.Of(() => Decode(token));
    }
}

/// <summary>Default <see cref="IAuthTokenDecoder"/>.</summary>
internal sealed class DefaultAuthTokenDecoder : IAuthTokenDecoder
{
    public AuthToken Decode(string encodedJwt)
    {
        try
        {
            return new AuthToken(new JsonWebToken(encodedJwt));
        }
        catch (ArgumentException e)
        {
            throw new AuthTokenAccessException("Failed to decode the access token.", e);
        }
    }
}
