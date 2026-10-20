using System.Text.Json;
using Sap.Cloud.Sdk.DataModel.OData.Adapter;
using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.OData.Helper;

/// <summary>
/// Deserializes the V2 envelope (<c>d</c>, <c>d.results</c>) of a buffered response into the typed model. Primitive results are
/// delegated to the OData client.
/// </summary>
/// <remarks>Replaces <c>ODataRequestResultGeneric.as(Class)</c> / <c>asList(Class)</c> with Gson for <see cref="VdmObject"/> types.</remarks>
internal static class ODataResponseReader
{
    private static bool IsVdmObject<T>() => typeof(VdmObject).IsAssignableFrom(typeof(T));

    private static ODataDeserializationException Error(ODataRequestResultGeneric result, string message, Exception? cause = null) =>
        new(result.ODataRequest, result.HttpResponse, message, cause);

    private static JsonDocument Parse(ODataRequestResultGeneric result)
    {
        if (!result.HasPayload)
        {
            throw Error(result, ODataProtocol.V2 + " response did not contain any payload.");
        }
        var text = result.HttpResponse.ReadBufferedText()
            ?? throw Error(result, ODataProtocol.V2 + " response has not been buffered and cannot be read as entity.");
        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException e)
        {
            throw Error(result, "A problem occurred while streaming the " + ODataProtocol.V2 + " response.", e);
        }
    }

    private static JsonElement GetD(ODataRequestResultGeneric result, JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("d", out var d))
        {
            return d;
        }
        throw Error(result, "Unable to read " + ODataProtocol.V2 + " response.");
    }

    /// <summary>Reads a single entity, complex object or primitive value.</summary>
    /// <param name="result">The result of the request.</param>
    /// <param name="functionName">For function imports, the name under which the result is wrapped in <c>d</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal static async Task<T> ReadSingleAsync<T>(ODataRequestResultGeneric result, string? functionName, CancellationToken cancellationToken)
    {
        if (!IsVdmObject<T>())
        {
            return await result.AsSingleAsync<T>(null, cancellationToken).ConfigureAwait(false);
        }
        return ReadSingleVdm<T>(result, functionName);
    }

    /// <summary>Reads a single entity or complex object from a buffered response.</summary>
    internal static T ReadSingleVdm<T>(ODataRequestResultGeneric result, string? functionName)
    {
        using var document = Parse(result);
        var element = GetD(result, document.RootElement);
        if (functionName is not null && element.ValueKind == JsonValueKind.Object && element.TryGetProperty(functionName, out var wrapped))
        {
            element = wrapped;
        }
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Error(result, "Unable to read " + ODataProtocol.V2 + " response.");
        }
        return Deserialize<T>(result, element, "Failed to deserialize a complex object.")
            ?? throw Error(result, "The " + ODataProtocol.V2 + " response did not contain an entity.");
    }

    /// <summary>Reads the entities of a result set.</summary>
    /// <param name="result">The result of the request.</param>
    /// <param name="functionName">For function imports, the name under which the result may be wrapped in <c>d</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal static async Task<IReadOnlyList<T>> ReadListAsync<T>(ODataRequestResultGeneric result, string? functionName, CancellationToken cancellationToken)
    {
        if (!IsVdmObject<T>())
        {
            return await result.AsListAsync<T>(cancellationToken).ConfigureAwait(false);
        }
        return ReadListVdm<T>(result, functionName);
    }

    /// <summary>Reads the entities of a result set from a buffered response.</summary>
    internal static IReadOnlyList<T> ReadListVdm<T>(ODataRequestResultGeneric result, string? functionName)
    {
        using var document = Parse(result);
        var d = GetD(result, document.RootElement);
        var set = FindResultSet(d) ?? (functionName is not null && d.ValueKind == JsonValueKind.Object && d.TryGetProperty(functionName, out var wrapped)
            ? FindResultSet(wrapped)
            : null);
        if (set is not { } array)
        {
            throw Error(result, "Unable to read " + ODataProtocol.V2 + " response.");
        }
        var list = new List<T>(array.GetArrayLength());
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                list.Add(Deserialize<T>(result, item, "Failed to parse " + ODataProtocol.V2 + " result to a list.")!);
            }
        }
        return list;
    }

    private static JsonElement? FindResultSet(JsonElement d)
    {
        if (d.ValueKind == JsonValueKind.Array)
        {
            return d;
        }
        if (d.ValueKind == JsonValueKind.Object && d.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            return results;
        }
        return null;
    }

    private static T? Deserialize<T>(ODataRequestResultGeneric result, JsonElement element, string message)
    {
        try
        {
            return element.Deserialize<T>(ODataVdmJsonOptions.Get());
        }
        catch (Exception e) when (e is JsonException or InvalidCastException or FormatException or InvalidOperationException)
        {
            throw Error(result, message, e);
        }
    }
}
