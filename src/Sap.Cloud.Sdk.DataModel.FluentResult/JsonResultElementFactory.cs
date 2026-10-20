using System.Text.Json;

namespace Sap.Cloud.Sdk.Result;

/// <summary>Creates <see cref="IResultElement"/>s from <see cref="JsonElement"/>s.</summary>
/// <remarks>Replaces <c>GsonResultElementFactory</c>; the <c>GsonBuilder</c> is replaced by <see cref="JsonSerializerOptions"/>.</remarks>
public class JsonResultElementFactory : IResultElementFactory<JsonElement?>
{
    /// <summary>Initializes a new instance of the <see cref="JsonResultElementFactory"/> class.</summary>
    /// <param name="serializerOptions">The options used to deserialize result objects. Create once and cache.</param>
    public JsonResultElementFactory(JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(serializerOptions);
        SerializerOptions = serializerOptions;
    }

    /// <summary>The options used to deserialize result objects.</summary>
    public JsonSerializerOptions SerializerOptions { get; }

    /// <summary>Creates a primitive.</summary>
    /// <param name="resultElement">The JSON value.</param>
    /// <returns>The primitive.</returns>
    protected virtual IResultPrimitive NewPrimitive(JsonElement resultElement) => new JsonResultPrimitive(resultElement);

    /// <summary>Creates an object.</summary>
    /// <param name="resultElement">The JSON value.</param>
    /// <returns>The object.</returns>
    protected virtual IResultObject NewObject(JsonElement resultElement) => new JsonResultObject(resultElement, this);

    /// <summary>Creates a collection.</summary>
    /// <param name="resultElement">The JSON value.</param>
    /// <returns>The collection.</returns>
    protected virtual IResultCollection NewCollection(JsonElement resultElement)
    {
        var elements = new List<IResultElement>();
        foreach (var item in resultElement.EnumerateArray())
        {
            elements.Add(Create(item)!);
        }
        return new DefaultResultCollection(elements);
    }

    /// <inheritdoc />
    public IResultElement? Create(JsonElement? resultElement)
    {
        if (resultElement is not { } element)
        {
            return null;
        }
        return element.ValueKind switch
        {
            JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => NewPrimitive(element),
            JsonValueKind.Object => NewObject(element),
            JsonValueKind.Array => NewCollection(element),
            _ => throw new ArgumentException($"Failed to convert JsonElement {element.GetRawText()} to instance of IResultElement."),
        };
    }
}
