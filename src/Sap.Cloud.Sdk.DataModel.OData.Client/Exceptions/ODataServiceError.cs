using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.Result;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;

/// <summary>Describes a single error (or error detail) reported by an OData service.</summary>
public interface IODataServiceErrorDetails
{
    /// <summary>The error code.</summary>
    string? ODataCode { get; }

    /// <summary>The error message.</summary>
    string? ODataMessage { get; }

    /// <summary>The target of the error, for example a property name.</summary>
    string? Target { get; }
}

/// <summary>The error payload of an OData service response.</summary>
/// <remarks>Port of <c>ODataServiceError</c>. <c>getTarget()</c> returned an <c>Option</c> in Java; here it is a nullable string.</remarks>
public sealed class ODataServiceError : IODataServiceErrorDetails, IEquatable<ODataServiceError>
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<ODataServiceError>();
    private const string ErrorDetailsField = "errordetails";

    private IReadOnlyList<IODataServiceErrorDetails>? _details;
    private Dictionary<string, object?>? _innerError;

    /// <summary>Initializes a new instance of the <see cref="ODataServiceError"/> class.</summary>
    public ODataServiceError()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ODataServiceError"/> class.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The error message.</param>
    /// <param name="target">The target.</param>
    /// <param name="details">The error details.</param>
    /// <param name="innerError">The inner error.</param>
    public ODataServiceError(
        string? code,
        string? message,
        string? target,
        IReadOnlyList<IODataServiceErrorDetails>? details,
        IDictionary<string, object?>? innerError)
    {
        ODataCode = code;
        ODataMessage = message;
        Target = target;
        _details = details;
        _innerError = innerError is null ? null : new Dictionary<string, object?>(innerError);
    }

    /// <inheritdoc />
    [ElementName("code")]
    public string? ODataCode { get; init; }

    /// <inheritdoc />
    [ElementName("message")]
    [JsonConverter(typeof(MessageJsonConverter))]
    public string? ODataMessage { get; init; }

    /// <inheritdoc />
    [ElementName("target")]
    public string? Target { get; init; }

    /// <summary>The error details; empty if there are none.</summary>
    [ElementName("details")]
    [JsonConverter(typeof(DetailsJsonConverter))]
    public IReadOnlyList<IODataServiceErrorDetails> Details
    {
        get => _details ?? [];
        init => _details = value;
    }

    /// <summary>The inner error; empty if there is none.</summary>
    [ElementName("innererror")]
    public IDictionary<string, object?> InnerError
    {
        get => _innerError ??= [];
        init => _innerError = value is null ? null : new Dictionary<string, object?>(value);
    }

    /// <summary>Creates the error from the <c>error</c> object of a response.</summary>
    /// <param name="resultObject">The <c>error</c> object.</param>
    /// <param name="protocol">The protocol.</param>
    /// <returns>The error.</returns>
    /// <exception cref="NotSupportedException">The object cannot be interpreted.</exception>
    public static ODataServiceError FromResultObject(IResultObject resultObject, IODataProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(resultObject);
        ArgumentNullException.ThrowIfNull(protocol);
        if (protocol.IsEqualTo(ODataProtocol.V4))
        {
            return resultObject.AsType<ODataServiceError>();
        }

        IReadOnlyList<ODataServiceError>? details = null;
        if (resultObject.GetElement("innererror") is { IsResultObject: true } innerError
            && innerError.GetAsObject().GetElement(ErrorDetailsField) is { IsResultCollection: true } detailsElement)
        {
            try
            {
                details = detailsElement.GetAsCollection().AsList<ODataServiceError>();
            }
#pragma warning disable CA1031 // As in Java: an uninterpretable "errordetails" list leaves the details empty.
            catch (Exception e)
            {
                Log.Debug(
                    e,
                    "Could not interpret the \"errordetails\" field of the {Protocol} error as a list of OData errors. The list of details on the OData error will be empty.",
                    protocol);
            }
#pragma warning restore CA1031
        }

        var odataError = resultObject.AsType<ODataServiceError>();
        if (details is null)
        {
            return odataError;
        }
        odataError.InnerError.Remove(ErrorDetailsField);
        odataError._details = details.Cast<IODataServiceErrorDetails>().ToList();
        return odataError;
    }

    /// <inheritdoc />
    public bool Equals(ODataServiceError? other) =>
        other is not null
        && ODataCode == other.ODataCode
        && ODataMessage == other.ODataMessage
        && Target == other.Target
        && Details.SequenceEqual(other.Details);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ODataServiceError);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(ODataCode, ODataMessage, Target);

    /// <inheritdoc />
    public override string ToString() =>
        $"ODataServiceError(ODataCode={ODataCode}, ODataMessage={ODataMessage}, Target={Target}, Details=[{string.Join(", ", Details)}])";

    private sealed class MessageJsonConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var json = document.RootElement;
            switch (json.ValueKind)
            {
                case JsonValueKind.String:
                    return json.GetString();
                case JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False:
                    return json.GetRawText();
                case JsonValueKind.Object:
                    if (json.TryGetProperty("value", out var value)
                        && value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                    {
                        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
                    }
                    Log.Warn("Unable to deserialize error value from \"message\": {Value}", value);
                    return null;
                default:
                    Log.Warn("Unable to deserialize a \"message\" value from JSON value: {Json}", json);
                    return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }

    private sealed class DetailsJsonConverter : JsonConverter<IReadOnlyList<IODataServiceErrorDetails>>
    {
        public override IReadOnlyList<IODataServiceErrorDetails> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return [];
            }
            var list = JsonSerializer.Deserialize<List<ODataServiceError>>(ref reader, options);
            return list is null ? [] : list.Cast<IODataServiceErrorDetails>().ToList();
        }

        public override void Write(
            Utf8JsonWriter writer,
            IReadOnlyList<IODataServiceErrorDetails> value,
            JsonSerializerOptions options) =>
            throw new NotSupportedException("Serialization of OData service errors is not supported.");
    }
}
