namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>A destination: a named set of properties describing a remote system.</summary>
/// <remarks>Port of <c>Destination</c>. The RFC conversions (<c>asRfc</c>, <c>isRfc</c>) are not ported.</remarks>
public interface IDestination : IDestinationProperties
{
}

/// <summary><c>asHttp</c> and <c>isHttp</c> of the Java <c>Destination</c> interface.</summary>
public static class DestinationExtensions
{
    /// <summary>Converts a destination to an HTTP destination.</summary>
    /// <param name="destination">The destination.</param>
    /// <returns>The destination itself if it already is an <see cref="IHttpDestination"/>, otherwise a converted copy.</returns>
    /// <exception cref="ArgumentException">The destination cannot be converted, for example because it has no valid URL.</exception>
    public static IHttpDestination AsHttp(this IDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return destination as IHttpDestination ?? DefaultHttpDestination.FromDestination(destination).Build();
    }

    /// <summary>Whether the destination is, or can be converted to, an HTTP destination.</summary>
    /// <param name="destination">The destination.</param>
    /// <returns><see langword="true"/> if <see cref="AsHttp"/> would succeed.</returns>
    public static bool IsHttp(this IDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return destination is IHttpDestination || DefaultHttpDestination.CanBeConstructedFrom(destination);
    }
}
