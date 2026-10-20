namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>How a user or refresh token is forwarded to the destination service.</summary>
internal enum TokenForwarding
{
    UserToken,
    RefreshToken,
    None,
}

/// <summary>
/// Describes one request to the destination service: on whose behalf it is sent, which token is forwarded in a header,
/// and the fragment and custom headers. Two instances are equal if all of that is equal.
/// </summary>
/// <remarks>The token is never printed by <see cref="ToString"/>.</remarks>
internal sealed class DestinationRetrievalStrategy : IEquatable<DestinationRetrievalStrategy>
{
    private readonly List<Header> _additionalHeaders = [];

    private DestinationRetrievalStrategy(OnBehalfOf behalf, TokenForwarding tokenForwarding, string? token)
    {
        Behalf = behalf;
        TokenForwarding = tokenForwarding;
        Token = token;
    }

    public OnBehalfOf Behalf { get; }

    public TokenForwarding TokenForwarding { get; }

    public string? Token { get; }

    public string? Fragment { get; private set; }

    public IReadOnlyList<Header> AdditionalHeaders => _additionalHeaders;

    public static DestinationRetrievalStrategy WithoutToken(OnBehalfOf behalf) =>
        new(behalf, TokenForwarding.None, null);

    public static DestinationRetrievalStrategy WithUserToken(OnBehalfOf behalf, string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return string.IsNullOrWhiteSpace(token)
            ? throw new ArgumentException("User token must not be empty.", nameof(token))
            : new DestinationRetrievalStrategy(behalf, TokenForwarding.UserToken, token);
    }

    public static DestinationRetrievalStrategy WithRefreshToken(OnBehalfOf behalf, string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return string.IsNullOrWhiteSpace(token)
            ? throw new ArgumentException("Refresh token must not be empty.", nameof(token))
            : new DestinationRetrievalStrategy(behalf, TokenForwarding.RefreshToken, token);
    }

    public DestinationRetrievalStrategy WithFragmentName(string fragmentName)
    {
        ArgumentNullException.ThrowIfNull(fragmentName);
        if (string.IsNullOrWhiteSpace(fragmentName))
        {
            throw new ArgumentException("Fragment name must not be empty", nameof(fragmentName));
        }
        // sanity check to enforce this is only ever set once
        if (Fragment is not null)
        {
            throw new InvalidOperationException("Attempted to change an already set fragment name");
        }
        Fragment = fragmentName;
        return this;
    }

    public DestinationRetrievalStrategy WithAdditionalHeaders(IEnumerable<Header> additionalHeaders)
    {
        ArgumentNullException.ThrowIfNull(additionalHeaders);
        _additionalHeaders.AddRange(additionalHeaders);
        return this;
    }

    public bool Equals(DestinationRetrievalStrategy? other) =>
        other is not null
        && Behalf == other.Behalf
        && TokenForwarding == other.TokenForwarding
        && string.Equals(Token, other.Token, StringComparison.Ordinal)
        && string.Equals(Fragment, other.Fragment, StringComparison.Ordinal)
        && _additionalHeaders.SequenceEqual(other._additionalHeaders);

    public override bool Equals(object? obj) => Equals(obj as DestinationRetrievalStrategy);

    public override int GetHashCode() => HashCode.Combine(Behalf, TokenForwarding, Token);

    public override string ToString() =>
        $"DestinationRetrievalStrategy(Behalf={Behalf}, TokenForwarding={TokenForwarding}, Token=(hidden), "
        + $"Fragment={Fragment}, AdditionalHeaders=[{string.Join(", ", _additionalHeaders)}])";
}
