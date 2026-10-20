using System.Numerics;
using System.Text.Json;

namespace Sap.Cloud.Sdk.Result;

/// <summary>A <see cref="IResultObject"/> backed by a JSON object.</summary>
/// <remarks>Replaces <c>GsonResultObject</c>; the Gson builder is replaced by <see cref="JsonSerializerOptions"/>.</remarks>
public class JsonResultObject : IResultObject
{
    private const string NotPrimitive = "Cannot get an object as primitive.";

    /// <summary>Initializes a new instance of the <see cref="JsonResultObject"/> class.</summary>
    /// <param name="jsonObject">The JSON object.</param>
    /// <param name="resultElementFactory">The factory used for children and deserialization.</param>
    public JsonResultObject(JsonElement jsonObject, JsonResultElementFactory resultElementFactory)
    {
        ArgumentNullException.ThrowIfNull(resultElementFactory);
        if (jsonObject.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("JSON value is not an object.", nameof(jsonObject));
        }
        JsonObject = jsonObject.Clone();
        ResultElementFactory = resultElementFactory;
    }

    /// <summary>The underlying JSON object.</summary>
    public JsonElement JsonObject { get; }

    /// <summary>The factory used for children and deserialization.</summary>
    public JsonResultElementFactory ResultElementFactory { get; }

    /// <inheritdoc />
    public bool IsResultPrimitive => false;

    /// <inheritdoc />
    public bool IsResultCollection => false;

    /// <inheritdoc />
    public bool IsResultObject => true;

    /// <inheritdoc />
    public IResultPrimitive GetAsPrimitive() => throw new NotSupportedException("Cannot get an object as primitive.");

    /// <inheritdoc />
    public IResultCollection GetAsCollection() => throw new NotSupportedException("Cannot get an object as collection.");

    /// <inheritdoc />
    public IResultObject GetAsObject() => this;

    /// <inheritdoc />
    public bool AsBoolean() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public byte AsByte() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public char AsCharacter() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public string AsString() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public int AsInteger() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public short AsShort() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public long AsLong() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public float AsFloat() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public double AsDouble() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public BigInteger AsBigInteger() => throw new NotSupportedException(NotPrimitive);

    /// <inheritdoc />
    public decimal AsBigDecimal() => throw new NotSupportedException(NotPrimitive);

#pragma warning disable CA1031 // Failures are reported uniformly as NotSupportedException, as in Java.
    /// <inheritdoc />
    public IResultElement? GetElement(string elementName)
    {
        ArgumentNullException.ThrowIfNull(elementName);
        try
        {
            return JsonObject.TryGetProperty(elementName, out var child) ? ResultElementFactory.Create(child) : null;
        }
        catch (Exception e)
        {
            throw new NotSupportedException($"Failed to get element with name {elementName}.", e);
        }
    }

    /// <inheritdoc />
    public T AsType<T>() => (T)AsType(typeof(T));

    /// <inheritdoc />
    public object AsType(Type objectType)
    {
        ArgumentNullException.ThrowIfNull(objectType);
        try
        {
            return JsonObject.Deserialize(objectType, ResultElementFactory.SerializerOptions)
                ?? throw new InvalidOperationException("Deserialization returned null.");
        }
        catch (Exception e)
        {
            throw new NotSupportedException($"Failed to get element as object of type {objectType.FullName}.", e);
        }
    }
#pragma warning restore CA1031

    /// <inheritdoc />
    public override string ToString() => $"JsonResultObject(JsonObject={JsonObject.GetRawText()})";
}
