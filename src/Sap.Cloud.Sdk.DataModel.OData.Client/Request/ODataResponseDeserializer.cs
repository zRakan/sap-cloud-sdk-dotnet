using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Request;

/// <summary>Locates the parts of an OData response payload according to the protocol's response descriptor.</summary>
internal sealed class ODataResponseDeserializer(IODataProtocol protocol)
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<ODataResponseDeserializer>();

    internal JsonElement? GetElementToResultSet(JsonElement element) =>
        GetResultJsonElement(element, protocol.PathToResultSet) is { ValueKind: JsonValueKind.Array } e ? e : null;

    internal JsonElement? GetElementToResultSingle(JsonElement element) =>
        GetResultJsonElement(element, protocol.PathToResultSingle) is { ValueKind: JsonValueKind.Object } e ? e : null;

    internal JsonElement? GetElementToResultPrimitiveSet(JsonElement element) =>
        GetResultJsonElement(element, protocol.PathToResultPrimitive) is { ValueKind: JsonValueKind.Array } e ? e : null;

    internal JsonElement? GetElementToResultPrimitiveSingle(JsonElement element) =>
        GetResultJsonElement(element, protocol.PathToResultPrimitive) is
        { ValueKind: JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False } e
            ? e
            : null;

    private static JsonElement? GetResultJsonElement(JsonElement element, JsonLookup lookup)
    {
        foreach (var path in lookup.Paths)
        {
            var result = GetResultJsonElement(element, path);
            if (result is not null)
            {
                return result;
            }
        }
        return null;
    }

    private static JsonElement? GetResultJsonElement(JsonElement element, JsonPath path)
    {
        var nodes = path.Nodes;
        JsonElement? resultElement = element;
        for (var i = 0; i < nodes.Count; i++)
        {
            if (resultElement is not { ValueKind: JsonValueKind.Object } current)
            {
                Log.Warn("JSON path {Path} could not be resolved for {Element} at position {Position}.", path, resultElement, i);
                return null;
            }
            if (nodes[i] == JsonPath.Wildcard)
            {
                using var enumerator = current.EnumerateObject().GetEnumerator();
                if (enumerator.MoveNext())
                {
                    resultElement = enumerator.Current.Value;
                }
                else
                {
                    Log.Warn("Wildcard in JSON path {Path}  did not match anything for {Element}.", path, current);
                    return null;
                }
            }
            else
            {
                resultElement = current.TryGetProperty(nodes[i], out var child) ? child : null;
            }
        }
        return resultElement;
    }
}

/// <summary>Parses the HTTP entity of a result as JSON.</summary>
internal static class HttpEntityReader
{
    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(HttpEntityReader).FullName!);

    internal static async Task<T> ReadAsync<T>(
        IODataRequestResult result,
        Func<JsonElement, T> elementConsumer,
        CancellationToken cancellationToken)
    {
        var httpResponse = result.HttpResponse;
        var request = result.ODataRequest;
        var protocol = request.Protocol;

        ODataDeserializationException Error(string message, Exception? cause) => new(request, httpResponse, message, cause);

        var stream = httpResponse.OpenContent();
        if (stream is null)
        {
            var msg = protocol + " response does not contain an HTTP entity.";
            Log.Warn("{Message}", msg);
            throw Error(msg, null);
        }
        try
        {
            await using (stream.ConfigureAwait(false))
            {
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                return elementConsumer(document.RootElement);
            }
        }
        catch (IOException e)
        {
            var msg = protocol + " response stream cannot be read for HTTP entity: ";
            Log.Debug(e, "{Message}", msg);
            throw Error(msg + nameof(ODataHttpResponse), e);
        }
#pragma warning disable CA1031 // As in Java, any failure while reading the entity becomes a deserialization exception.
        catch (Exception e) when (e is not OperationCanceledException and not ODataException)
        {
            var msg = "A problem occurred while streaming the " + protocol + " response.";
            Log.Debug(e, "{Message}", msg);
            throw Error(msg, e);
        }
#pragma warning restore CA1031
    }
}
