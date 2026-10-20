using System.Security.Cryptography.X509Certificates;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Compares key stores by the certificates they contain (private keys are not compared).</summary>
internal static class DestinationKeyStoreComparator
{
    private const int InitialHashCode = 17;

    public static int ResolveKeyStoreHashCode(X509Certificate2Collection? keyStore)
    {
        var hash = InitialHashCode;
        foreach (var certificate in ResolveCertificatesOnly(keyStore))
        {
            hash = unchecked((hash * 37) + certificate.GetHashCode());
        }
        return hash;
    }

    public static IReadOnlyList<X509Certificate2> ResolveCertificatesOnly(X509Certificate2Collection? keyStore) =>
        keyStore is null ? [] : [.. keyStore];

    public static bool KeyStoresEqual(X509Certificate2Collection? a, X509Certificate2Collection? b)
    {
        var left = ResolveCertificatesOnly(a);
        var right = ResolveCertificatesOnly(b);
        return left.Count == right.Count && left.Zip(right).All(p => p.First.RawData.AsSpan().SequenceEqual(p.Second.RawData));
    }
}
