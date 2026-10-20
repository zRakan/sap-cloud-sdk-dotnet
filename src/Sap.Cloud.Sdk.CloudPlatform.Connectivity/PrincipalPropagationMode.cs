namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>How the principal is propagated to an on-premise system.</summary>
public enum PrincipalPropagationMode
{
    /// <summary>The user token is exchanged for a token for the connectivity service.</summary>
    TokenExchange,

    /// <summary>The user token is forwarded to the Cloud Connector.</summary>
    TokenForwarding,

    /// <summary>The mode could not be determined.</summary>
    Unknown,
}

/// <summary>Identifier conversions for <see cref="PrincipalPropagationMode"/>.</summary>
public static class PrincipalPropagationModeExtensions
{
    /// <summary>Gets the identifier used in destination properties: <c>TOKEN_EXCHANGE</c>, <c>TOKEN_FORWARDING</c> or <c>UNKNOWN</c>.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The identifier.</returns>
    public static string GetIdentifier(this PrincipalPropagationMode mode) => mode switch
    {
        PrincipalPropagationMode.TokenExchange => "TOKEN_EXCHANGE",
        PrincipalPropagationMode.TokenForwarding => "TOKEN_FORWARDING",
        PrincipalPropagationMode.Unknown => "UNKNOWN",
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    /// <summary>Parses an identifier (trimmed, case-insensitive). Unrecognized values yield <see cref="PrincipalPropagationMode.Unknown"/>.</summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>The mode.</returns>
    public static PrincipalPropagationMode OfIdentifier(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        var trimmed = identifier.Trim();
        foreach (var mode in Enum.GetValues<PrincipalPropagationMode>())
        {
            if (string.Equals(mode.GetIdentifier(), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return mode;
            }
        }
        return PrincipalPropagationMode.Unknown;
    }
}
