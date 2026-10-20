using System.Globalization;
using System.Numerics;
using System.Text.Json;

namespace Sap.Cloud.Sdk.Result;

/// <summary>A <see cref="IResultPrimitive"/> backed by a <see cref="JsonElement"/>.</summary>
/// <remarks>Replaces <c>GsonResultPrimitive</c>.</remarks>
public class JsonResultPrimitive : IResultPrimitive, IEquatable<JsonResultPrimitive>
{
    /// <summary>Initializes a new instance of the <see cref="JsonResultPrimitive"/> class.</summary>
    /// <param name="jsonPrimitive">The JSON string, number or boolean.</param>
    public JsonResultPrimitive(JsonElement jsonPrimitive)
    {
        if (jsonPrimitive.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
        {
            throw new ArgumentException($"JSON value of kind {jsonPrimitive.ValueKind} is not a primitive.", nameof(jsonPrimitive));
        }
        JsonPrimitive = jsonPrimitive.Clone();
    }

    /// <summary>The underlying JSON value.</summary>
    public JsonElement JsonPrimitive { get; }

    /// <inheritdoc />
    public bool IsResultPrimitive => true;

    /// <inheritdoc />
    public bool IsResultCollection => false;

    /// <inheritdoc />
    public bool IsResultObject => false;

    /// <inheritdoc />
    public IResultPrimitive GetAsPrimitive() => this;

    /// <inheritdoc />
    public IResultCollection GetAsCollection() => throw new NotSupportedException("Cannot get a primitive as collection.");

    /// <inheritdoc />
    public IResultObject GetAsObject() => throw new NotSupportedException("Cannot get a primitive as object.");

    /// <inheritdoc />
    public bool AsBoolean() => Convert(p => p.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => string.Equals(p.GetString(), "true", StringComparison.OrdinalIgnoreCase),
        _ => p.GetDouble() != 0,
    });

    /// <inheritdoc />
    public byte AsByte() => Convert(p => p.ValueKind == JsonValueKind.Number && p.TryGetByte(out var b)
        ? b
        : byte.Parse(Text(p), NumberStyles.Integer, CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public char AsCharacter() => Convert(p => Text(p)[0]);

    /// <inheritdoc />
    public string AsString() => Convert(Text);

    /// <inheritdoc />
    public int AsInteger() => Convert(p => p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i)
        ? i
        : int.Parse(Text(p), NumberStyles.Integer, CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public short AsShort() => Convert(p => p.ValueKind == JsonValueKind.Number && p.TryGetInt16(out var i)
        ? i
        : short.Parse(Text(p), NumberStyles.Integer, CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public long AsLong() => Convert(p => p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var i)
        ? i
        : long.Parse(Text(p), NumberStyles.Integer, CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public float AsFloat() => Convert(p => float.Parse(Text(p), NumberStyles.Float, CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public double AsDouble() => Convert(p => double.Parse(Text(p), NumberStyles.Float, CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public BigInteger AsBigInteger() => Convert(p => BigInteger.Parse(Text(p), NumberStyles.Integer, CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public decimal AsBigDecimal() => Convert(p => decimal.Parse(Text(p), NumberStyles.Float, CultureInfo.InvariantCulture));

    /// <inheritdoc />
    public bool Equals(JsonResultPrimitive? other) =>
        other is not null && JsonPrimitive.GetRawText() == other.JsonPrimitive.GetRawText();

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as JsonResultPrimitive);

    /// <inheritdoc />
    public override int GetHashCode() => JsonPrimitive.GetRawText().GetHashCode(StringComparison.Ordinal);

    /// <inheritdoc />
    public override string ToString() => $"JsonResultPrimitive(JsonPrimitive={JsonPrimitive.GetRawText()})";

    private static string Text(JsonElement p) => p.ValueKind switch
    {
        JsonValueKind.String => p.GetString()!,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => p.GetRawText(),
    };

#pragma warning disable CA1031 // Any conversion failure is reported uniformly as NotSupportedException, as in Java.
    private T Convert<T>(Func<JsonElement, T> convert)
    {
        try
        {
            return convert(JsonPrimitive);
        }
        catch (Exception e)
        {
            throw new NotSupportedException(e.Message, e);
        }
    }
#pragma warning restore CA1031
}
