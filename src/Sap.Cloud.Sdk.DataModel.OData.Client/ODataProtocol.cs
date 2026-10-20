using System.Globalization;
using System.Numerics;

namespace Sap.Cloud.Sdk.DataModel.OData.Client;

/// <summary>The supported OData protocol versions.</summary>
public static class ODataProtocol
{
    /// <summary>OData V2.</summary>
    public static IODataProtocol V2 { get; } = new ODataProtocolV2();

    /// <summary>OData V4.</summary>
    public static IODataProtocol V4 { get; } = new ODataProtocolV4();

    private sealed class ODataProtocolV2 : IODataProtocol
    {
        public string ProtocolVersion => "2.0";

        public JsonLookup PathToResultSet { get; } = JsonLookup.Of(JsonPath.Of("d", "results"));

        public JsonLookup PathToResultSingle { get; } = JsonLookup.Of(JsonPath.Of("d"));

        public JsonLookup PathToResultPrimitive { get; } = JsonLookup.Of(JsonPath.Of("d", JsonPath.Wildcard));

        public JsonLookup PathToInlineCount { get; } = JsonLookup.Of(JsonPath.Of("__count"));

        public JsonLookup PathToNextLink { get; } = JsonLookup.Of(JsonPath.Of("__next"));

        public JsonLookup PathToDeltaLink { get; } = JsonLookup.Empty();

        public Func<object, string> NumberSerializer { get; } = NumberToString;

        public Func<Guid, string> UuidSerializer { get; } = v => $"guid'{v.ToString("D", CultureInfo.InvariantCulture)}'";

        public Func<DateTimeOffset, string> DateTimeOffsetSerializer { get; } =
            v => $"datetimeoffset'{JavaFormat.ToIsoOffsetDateTime(v)}'";

        public Func<TimeOnly, string> TimeOfDaySerializer { get; } =
            v => $"time'{JavaFormat.ToIsoString(v.Ticks)}'";

        public Func<DateTime, string> DateTimeSerializer { get; } = v => $"datetime'{JavaFormat.ToEdmDateTime(v)}'";

        public KeyValuePair<string, string> GetQueryOptionInlineCount(bool optionEnabled) =>
            new("$inlinecount", optionEnabled ? "allpages" : "none");

        public override string ToString() => "OData " + ProtocolVersion;

        private static string NumberToString(object n) => n switch
        {
            int i => i.ToString(CultureInfo.InvariantCulture),
            short s => s.ToString(CultureInfo.InvariantCulture),
            byte b => b.ToString(CultureInfo.InvariantCulture),
            sbyte sb => sb.ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture) + "L",
            float f => JavaFormat.ToString(f) + "f",
            double d => JavaFormat.ToString(d) + "d",
            decimal m => m.ToString(CultureInfo.InvariantCulture) + "M",
            _ => throw new InvalidOperationException(
                $"Unrecognized number type: {n.GetType()}. Should be one of: Integer, Long, Float, Double, BigDecimal."),
        };
    }

    private sealed class ODataProtocolV4 : IODataProtocol
    {
        private static readonly HashSet<string> ExpandOptions = ["$count", "$filter", "$orderby", "$search", "$skip", "$top"];

        public string ProtocolVersion => "4.0";

        public JsonLookup PathToResultSet { get; } = JsonLookup.Of(JsonPath.Of("value"));

        public JsonLookup PathToResultSingle { get; } = JsonLookup.Of(JsonPath.OfRoot());

        public JsonLookup PathToResultPrimitive { get; } = JsonLookup.Of(JsonPath.Of("value"));

        public JsonLookup PathToInlineCount { get; } = JsonLookup.Of(JsonPath.Of("@odata.count"), JsonPath.Of("@count"));

        public JsonLookup PathToNextLink { get; } = JsonLookup.Of(JsonPath.Of("@odata.nextLink"), JsonPath.Of("@nextLink"));

        public JsonLookup PathToDeltaLink { get; } = JsonLookup.Of(JsonPath.Of("@odata.deltaLink"), JsonPath.Of("@deltaLink"));

        public Func<object, string> NumberSerializer { get; } = NumberToString;

        public Func<Guid, string> UuidSerializer { get; } = v => v.ToString("D", CultureInfo.InvariantCulture);

        public Func<DateTimeOffset, string> DateTimeOffsetSerializer { get; } = JavaFormat.ToIsoOffsetDateTime;

        public Func<TimeOnly, string> TimeOfDaySerializer { get; } = JavaFormat.ToIsoLocalTime;

        public Func<DateTime, string> DateTimeSerializer { get; } =
            v => JavaFormat.ToIsoOffsetDateTime(new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Unspecified), TimeSpan.Zero));

        public KeyValuePair<string, string> GetQueryOptionInlineCount(bool optionEnabled) =>
            new("$count", optionEnabled ? "true" : "false");

        public bool AllowCustomQueryParameter(bool isRoot, string key) => isRoot || ExpandOptions.Contains(key);

        public override string ToString() => "OData " + ProtocolVersion;

        private static string NumberToString(object n) => n switch
        {
            float f => JavaFormat.ToString(f),
            double d => JavaFormat.ToString(d),
            IFormattable formattable and (sbyte or byte or short or ushort or int or uint or long or ulong or decimal or BigInteger) =>
                formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Unrecognized number type: {n.GetType()}."),
        };
    }
}
