using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sap.Cloud.Sdk.DataModel.OData.Client.Adapter;
using Sap.Cloud.Sdk.Result;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Request;

/// <summary>How untyped JSON numbers are deserialized.</summary>
public enum NumberDeserializationStrategy
{
    /// <summary>As <see cref="double"/>.</summary>
    AsDouble,

    /// <summary>As <see cref="decimal"/> (Java: <c>BigDecimal</c>).</summary>
    AsDecimal,
}

/// <summary>Creates the <see cref="JsonSerializerOptions"/> used to (de)serialize OData payloads.</summary>
/// <remarks>Replaces <c>ODataGsonBuilder</c>. Only members annotated with <see cref="ElementNameAttribute"/> are (de)serialized.</remarks>
public static class ODataJsonOptions
{
    private static readonly JsonSerializerOptions DoubleOptions = Create(NumberDeserializationStrategy.AsDouble);
    private static readonly JsonSerializerOptions DecimalOptions = Create(NumberDeserializationStrategy.AsDecimal);

    /// <summary>Gets the shared options for the given number strategy. The instance is cached and must not be modified.</summary>
    /// <param name="numberStrategy">The number strategy.</param>
    /// <returns>The options.</returns>
    public static JsonSerializerOptions Get(NumberDeserializationStrategy numberStrategy = NumberDeserializationStrategy.AsDouble) =>
        numberStrategy == NumberDeserializationStrategy.AsDouble ? DoubleOptions : DecimalOptions;

    private static JsonSerializerOptions Create(NumberDeserializationStrategy numberStrategy)
    {
        var options = ElementNameJsonTypeInfoResolver.CreateOptions(onlyAnnotated: true);
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        // Gson reads numbers given as strings (for example "NaN" or "12") like numbers.
        options.NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals;
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new GuidJsonConverter());
        options.Converters.Add(new DateTimeOffsetJsonConverter());
        options.Converters.Add(new DurationJsonConverter());
        options.Converters.Add(new TimeOnlyJsonConverter());
        options.Converters.Add(new DateOnlyJsonConverter());
        options.Converters.Add(new BinaryJsonConverter());
        options.Converters.Add(new ObjectJsonConverter(numberStrategy == NumberDeserializationStrategy.AsDecimal));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
