using System.Text.Json;
using Sap.Cloud.Sdk.DataModel.OData.Client.Adapter;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Tests;

public class BinaryDecodingTests
{
    private static readonly byte[] Bytes = [0xfb, 0xff, 0xbf, 0x01];
    private static readonly JsonSerializerOptions Options = new() { Converters = { new BinaryJsonConverter() } };

    [Theory]
    [InlineData("+/+/AQ==")] // standard
    [InlineData("+/+/AQ")]   // standard, unpadded
    [InlineData("-_-_AQ==")] // URL-safe
    [InlineData("-_-_AQ")]   // URL-safe, unpadded
    public void DecodeFromString_AcceptsBothAlphabets(string encoded) =>
        Assert.Equal(Bytes, IValueBinary.DecodeFromString(encoded));

    [Fact]
    public void JsonConverter_ReadsUrlSafeBase64()
    {
        Assert.Equal(Bytes, JsonSerializer.Deserialize<byte[]>("\"-_-_AQ\"", Options));
    }
}
