using System.Globalization;
using System.Text.Json;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>The response of the destination service to a request for a single destination (<c>/v1/destinations/{name}</c> and <c>/v2/destinations/{name}</c>).</summary>
/// <remarks>
/// Port of <c>DestinationServiceV1Response</c>. Gson mapping is replaced by <see cref="Parse"/>. Authentication tokens
/// and certificates hold secrets and are hidden from <c>ToString</c>.
/// </remarks>
public sealed class DestinationServiceV1Response
{
    /// <summary>The owner of the destination; the <c>/v2</c> API does not return it.</summary>
    public DestinationOwner? Owner { get; init; }

    /// <summary>The properties of the destination.</summary>
    public IReadOnlyDictionary<string, string> DestinationConfiguration { get; init; } = new Dictionary<string, string>();

    /// <summary>The authentication tokens the destination service obtained, if any.</summary>
    public IReadOnlyList<DestinationAuthToken>? AuthTokens { get; init; }

    /// <summary>The certificates referenced by the destination, if any.</summary>
    public IReadOnlyList<DestinationCertificate>? Certificates { get; init; }

    /// <summary>Parses the JSON returned by the destination service.</summary>
    /// <param name="json">The JSON document.</param>
    /// <returns>The response.</returns>
    /// <exception cref="JsonException">The document is not valid JSON or has an unexpected structure.</exception>
    public static DestinationServiceV1Response Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The destination service response is not a JSON object.");
        }

        DestinationOwner? owner = null;
        Dictionary<string, string> configuration = [];
        List<DestinationAuthToken>? tokens = null;
        List<DestinationCertificate>? certificates = null;
        foreach (var property in root.EnumerateObject())
        {
            switch (property.Name)
            {
                case "owner" when property.Value.ValueKind == JsonValueKind.Object:
                    owner = new DestinationOwner
                    {
                        SubaccountId = GetString(property.Value, "SubaccountId"),
                        InstanceId = GetString(property.Value, "InstanceId"),
                    };
                    break;
                case "destinationConfiguration" when property.Value.ValueKind == JsonValueKind.Object:
                    foreach (var entry in property.Value.EnumerateObject())
                    {
                        if (ToText(entry.Value) is { } text)
                        {
                            configuration[entry.Name] = text;
                        }
                    }
                    break;
                case "authTokens" when property.Value.ValueKind == JsonValueKind.Array:
                    tokens = [.. property.Value.EnumerateArray().Select(ParseToken)];
                    break;
                case "certificates" when property.Value.ValueKind == JsonValueKind.Array:
                    certificates = [.. property.Value.EnumerateArray().Select(ParseCertificate)];
                    break;
                default:
                    break;
            }
        }
        return new DestinationServiceV1Response
        {
            Owner = owner,
            DestinationConfiguration = configuration,
            AuthTokens = tokens,
            Certificates = certificates,
        };
    }

    private static DestinationAuthToken ParseToken(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("An authentication token of the destination service response is not a JSON object.");
        }
        Header? header = null;
        if (element.TryGetProperty("http_header", out var headerElement) && headerElement.ValueKind == JsonValueKind.Object)
        {
            var name = GetString(headerElement, "key");
            if (name is not null)
            {
                header = new Header(name, GetString(headerElement, "value"));
            }
        }
        return new DestinationAuthToken
        {
            Type = GetString(element, "type"),
            Value = GetString(element, "value"),
            Error = GetString(element, "error"),
            ExpiresIn = GetString(element, "expires_in"),
            HttpHeaderSuggestion = header,
        };
    }

    private static DestinationCertificate ParseCertificate(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A certificate of the destination service response is not a JSON object.");
        }
        return new DestinationCertificate
        {
            Name = GetString(element, "Name"),
            Content = GetString(element, "Content"),
            Type = GetString(element, "Type"),
        };
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? ToText(value) : null;

    // Gson converts numbers and booleans to strings when a String is expected.
    private static string? ToText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => null,
    };

    /// <summary>The owner of a destination: a subaccount and, for instance level destinations, a service instance.</summary>
    public sealed class DestinationOwner
    {
        /// <summary>The subaccount identifier.</summary>
        public string? SubaccountId { get; init; }

        /// <summary>The service instance identifier.</summary>
        public string? InstanceId { get; init; }
    }

    /// <summary>An authentication token the destination service obtained for a destination, with an optional error and expiration.</summary>
    /// <remarks>
    /// Instances are part of the destination's properties and therefore of cache keys of HTTP clients; equality compares
    /// all values except <see cref="ExpiryTimestamp"/>. <see cref="ToString"/> hides the token.
    /// </remarks>
    public sealed class DestinationAuthToken : IEquatable<DestinationAuthToken>
    {
        /// <summary>The token type, for example <c>Bearer</c>. Only used for legacy fallbacks.</summary>
        public string? Type { get; init; }

        /// <summary>The token value. Confidential.</summary>
        public string? Value { get; init; }

        /// <summary>The error message if the destination service failed to obtain the token.</summary>
        public string? Error { get; init; }

        /// <summary>The lifetime of the token in seconds, as returned by the service. May be empty.</summary>
        public string? ExpiresIn { get; init; }

        /// <summary>The header with which the token should be sent.</summary>
        public Header? HttpHeaderSuggestion { get; init; }

        /// <summary>The point in time at which the token expires, derived from <see cref="ExpiresIn"/> when the destination is created.</summary>
        public DateTimeOffset? ExpiryTimestamp { get; internal set; }

        /// <inheritdoc/>
        public bool Equals(DestinationAuthToken? other) =>
            other is not null
            && string.Equals(Type, other.Type, StringComparison.Ordinal)
            && string.Equals(Value, other.Value, StringComparison.Ordinal)
            && string.Equals(Error, other.Error, StringComparison.Ordinal)
            && string.Equals(ExpiresIn, other.ExpiresIn, StringComparison.Ordinal)
            && Equals(HttpHeaderSuggestion, other.HttpHeaderSuggestion);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as DestinationAuthToken);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Type, Value, Error, ExpiresIn, HttpHeaderSuggestion);

        /// <summary>Prints the metadata, never the token.</summary>
        /// <returns>A string representation.</returns>
        public override string ToString() =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"DestinationAuthToken(Type={Type}, Value=(hidden), Error={(Error is null ? "none" : "present")}, ExpiresIn={ExpiresIn}, ExpiryTimestamp={ExpiryTimestamp:O})");
    }

    /// <summary>A named destination certificate with optional base64 encoded binary content.</summary>
    /// <remarks>Equality compares all values except <see cref="ExpiryTimestamp"/>. <see cref="ToString"/> hides the content.</remarks>
    public sealed class DestinationCertificate : IEquatable<DestinationCertificate>
    {
        /// <summary>The file name, for example <c>client.p12</c>. The extension determines the format.</summary>
        public string? Name { get; init; }

        /// <summary>The base64 encoded file content. Confidential.</summary>
        public string? Content { get; init; }

        /// <summary>The certificate type as reported by the destination service.</summary>
        public string? Type { get; init; }

        /// <summary>
        /// A point in time after which the SDK no longer reuses the cached destination because changes to certificates
        /// cannot be detected otherwise. It is not the expiration of the certificate itself.
        /// </summary>
        public DateTimeOffset? ExpiryTimestamp { get; internal set; }

        /// <inheritdoc/>
        public bool Equals(DestinationCertificate? other) =>
            other is not null
            && string.Equals(Name, other.Name, StringComparison.Ordinal)
            && string.Equals(Content, other.Content, StringComparison.Ordinal)
            && string.Equals(Type, other.Type, StringComparison.Ordinal);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as DestinationCertificate);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Name, Content, Type);

        /// <summary>Prints the name and type, never the content.</summary>
        /// <returns>A string representation.</returns>
        public override string ToString() =>
            string.Create(CultureInfo.InvariantCulture, $"DestinationCertificate(Name={Name}, Content=(hidden), Type={Type})");
    }
}
