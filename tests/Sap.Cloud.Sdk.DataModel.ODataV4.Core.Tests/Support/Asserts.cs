using System.Text.Json.Nodes;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core.Tests.Support;

/// <summary>Assertions on relative URIs and JSON payloads.</summary>
internal static class Asserts
{
    /// <summary>Asserts that the (decoded) query of the URI contains a parameter with the value.</summary>
    public static void HasParameter(Uri relativeUri, string name, string expectedValue)
    {
        var parameters = ParseQuery(relativeUri);
        Assert.True(parameters.TryGetValue(name, out var actual), $"Parameter '{name}' is missing in '{relativeUri.OriginalString}'.");
        Assert.Equal(expectedValue, actual);
    }

    /// <summary>Asserts that the URI has no query parameter of the name.</summary>
    public static void HasNoParameter(Uri relativeUri, string name) =>
        Assert.False(ParseQuery(relativeUri).ContainsKey(name));

    public static Dictionary<string, string> ParseQuery(Uri relativeUri)
    {
        var text = relativeUri.OriginalString;
        var index = text.IndexOf('?', StringComparison.Ordinal);
        var result = new Dictionary<string, string>();
        if (index < 0)
        {
            return result;
        }

        foreach (var pair in text[(index + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            var key = Uri.UnescapeDataString(separator < 0 ? pair : pair[..separator]);
            var value = separator < 0 ? string.Empty : Uri.UnescapeDataString(pair[(separator + 1)..]);
            result[key] = value;
        }

        return result;
    }

    /// <summary>The request path of the URI without query.</summary>
    public static string Path(Uri relativeUri)
    {
        var text = relativeUri.OriginalString;
        var index = text.IndexOf('?', StringComparison.Ordinal);
        return index < 0 ? text : text[..index];
    }

    /// <summary>Asserts that two JSON texts are equal; the order of properties is irrelevant.</summary>
    public static void JsonEqual(string expected, string actual)
    {
        var expectedNode = JsonNode.Parse(expected);
        var actualNode = JsonNode.Parse(actual);
        Assert.True(JsonNode.DeepEquals(expectedNode, actualNode), $"Expected: {expected}{Environment.NewLine}Actual:   {actual}");
    }

    /// <summary>
    /// Asserts that the actual JSON contains everything of the expected JSON (extra properties are allowed, the order of
    /// array items is irrelevant), like JSONassert's <c>LENIENT</c> mode.
    /// </summary>
    public static void JsonLenient(string expected, string actual)
    {
        var expectedNode = JsonNode.Parse(expected);
        var actualNode = JsonNode.Parse(actual);
        Assert.True(Contains(actualNode, expectedNode), $"Expected (lenient): {expected}{Environment.NewLine}Actual:            {actual}");
    }

    private static bool Contains(JsonNode? actual, JsonNode? expected)
    {
        switch (expected)
        {
            case null:
                return actual is null;
            case JsonObject expectedObject:
                return actual is JsonObject actualObject
                    && expectedObject.All(property => actualObject.TryGetPropertyValue(property.Key, out var value) && Contains(value, property.Value));
            case JsonArray expectedArray:
                if (actual is not JsonArray actualArray || actualArray.Count != expectedArray.Count)
                {
                    return false;
                }

                var remaining = actualArray.ToList();
                foreach (var item in expectedArray)
                {
                    var match = remaining.FindIndex(candidate => Contains(candidate, item));
                    if (match < 0)
                    {
                        return false;
                    }

                    remaining.RemoveAt(match);
                }

                return true;
            default:
                return JsonNode.DeepEquals(actual, expected);
        }
    }

    /// <summary>Gets the sent payload of an update, create or action request.</summary>
    public static string Payload(ODataRequestUpdate request) => request.SerializedEntity;
}
