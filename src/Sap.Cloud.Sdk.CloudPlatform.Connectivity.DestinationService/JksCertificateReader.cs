using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>
/// Reads the certificates from a Java key store (JKS) file. .NET has no JKS support, so the destination service's trust
/// stores in JKS format are handled by reading the (public) certificates directly. Private keys are not read.
/// </summary>
/// <remarks>
/// Like the trust managers of the JDK, this returns the trusted certificate entries and, for private key entries, the first
/// certificate of their chain. The integrity digest is not verified (the store password is not needed to read public data).
/// </remarks>
internal static class JksCertificateReader
{
    private const uint Magic = 0xFEEDFEED;
    private const int PrivateKeyEntry = 1;
    private const int TrustedCertificateEntry = 2;

    /// <exception cref="DestinationAccessException">The data is not a JKS key store.</exception>
    public static X509Certificate2Collection ReadTrustedCertificates(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var certificates = new X509Certificate2Collection();
        try
        {
            var reader = new Reader(data);
            if (reader.ReadInt32() != unchecked((int)Magic))
            {
                throw new InvalidDataException("Not a JKS key store: the magic number does not match.");
            }
            var version = reader.ReadInt32();
            if (version is not (1 or 2))
            {
                throw new InvalidDataException("Unsupported JKS version: " + version);
            }
            var count = reader.ReadInt32();
            for (var i = 0; i < count; i++)
            {
                var tag = reader.ReadInt32();
                reader.SkipUtf(); // alias
                reader.Skip(8); // creation date
                switch (tag)
                {
                    case PrivateKeyEntry:
                        reader.Skip(reader.ReadInt32()); // the protected private key
                        var chainLength = reader.ReadInt32();
                        for (var c = 0; c < chainLength; c++)
                        {
                            var certificate = ReadCertificate(reader, version);
                            if (c == 0)
                            {
                                certificates.Add(certificate);
                            }
                            else
                            {
                                certificate.Dispose();
                            }
                        }
                        break;
                    case TrustedCertificateEntry:
                        certificates.Add(ReadCertificate(reader, version));
                        break;
                    default:
                        throw new InvalidDataException("Unknown JKS entry tag: " + tag);
                }
            }
            return certificates;
        }
        catch (Exception e) when (e is InvalidDataException or CryptographicException or EndOfStreamException)
        {
            foreach (var certificate in certificates)
            {
                certificate.Dispose();
            }
            throw new DestinationAccessException("Failed to load key store.", e);
        }
    }

    private static X509Certificate2 ReadCertificate(Reader reader, int version)
    {
        if (version == 2)
        {
            reader.SkipUtf(); // certificate type, X.509
        }
        return X509CertificateLoader.LoadCertificate(reader.ReadBytes(reader.ReadInt32()));
    }

    private sealed class Reader(byte[] data)
    {
        private int _position;

        public int ReadInt32()
        {
            Ensure(4);
            var value = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(_position, 4));
            _position += 4;
            return value;
        }

        public void Skip(int length)
        {
            Ensure(length);
            _position += length;
        }

        public void SkipUtf()
        {
            Ensure(2);
            var length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(_position, 2));
            _position += 2;
            Skip(length);
        }

        public byte[] ReadBytes(int length)
        {
            Ensure(length);
            var result = data.AsSpan(_position, length).ToArray();
            _position += length;
            return result;
        }

        private void Ensure(int length)
        {
            if (length < 0 || _position + length > data.Length)
            {
                throw new EndOfStreamException("The JKS key store is truncated.");
            }
        }
    }
}
