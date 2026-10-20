using System.Text;
using System.Text.Json;

namespace Sap.Cloud.Sdk.TestSupport;

/// <summary>Creates unsigned test JWTs. The SDK decodes tokens without validating them.</summary>
internal static class TestJwt
{
    public static string Create(object claims)
    {
        var header = Encode(new { alg = "none", typ = "JWT" });
        var payload = Encode(claims);
        return $"{header}.{payload}.c2lnbmF0dXJl";
    }

    public static string Create(IReadOnlyDictionary<string, object?> claims) => Create((object)claims);

    private static string Encode(object value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
