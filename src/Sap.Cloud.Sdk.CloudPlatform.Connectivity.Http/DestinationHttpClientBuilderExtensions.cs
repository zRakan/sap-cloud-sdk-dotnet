using Microsoft.Extensions.DependencyInjection;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Targets <c>IHttpClientFactory</c> clients at SAP destinations.</summary>
public static class DestinationHttpClientBuilderExtensions
{
    /// <summary>
    /// The base address of clients built by <c>AddSapDestination</c>. The destination is only known per request, so
    /// this placeholder lets <see cref="HttpClient"/> accept relative request URIs; <see cref="DestinationRequestHandler"/>
    /// replaces it by the destination URI. Never used for a connection.
    /// </summary>
    public static readonly Uri PlaceholderBaseAddress = new("https://sap-destination.invalid/");

    /// <summary>Makes the client send its requests to the destination with the given name.</summary>
    /// <param name="builder">The client builder.</param>
    /// <param name="destinationName">The name of the destination.</param>
    /// <returns>The builder.</returns>
    /// <remarks>
    /// The destination is looked up with <see cref="DestinationAccessor.GetDestinationAsync(string, CancellationToken)"/>
    /// for every request, because destinations can be tenant-specific. Use relative request URIs; they are appended to
    /// the path of the destination. The pipeline is <c>DestinationRequestHandler</c> (URI, headers), then
    /// <c>CsrfTokenHandler</c>, then a primary handler configured per destination (proxy, TLS, timeouts) and cached
    /// like the clients of <see cref="HttpClientAccessor"/>.
    /// </remarks>
    public static IHttpClientBuilder AddSapDestination(this IHttpClientBuilder builder, string destinationName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationName);
        return builder.AddSapDestinationCore(async cancellationToken =>
        {
            var destination = await DestinationAccessor.GetDestinationAsync(destinationName, cancellationToken).ConfigureAwait(false);
            return destination.AsHttp();
        });
    }

    /// <summary>Makes the client send its requests to a destination that is determined by a function.</summary>
    /// <param name="builder">The client builder.</param>
    /// <param name="destinationProvider">
    /// Returns the destination; called for every request with the service provider of the client's handler scope.
    /// </param>
    /// <returns>The builder.</returns>
    public static IHttpClientBuilder AddSapDestination(
        this IHttpClientBuilder builder,
        Func<IServiceProvider, IHttpDestinationProperties> destinationProvider)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(destinationProvider);
        return builder.AddHttpMessageHandler(serviceProvider =>
            new DestinationRequestHandler(
                _ => ValueTask.FromResult(destinationProvider(serviceProvider)),
                PlaceholderBaseAddress))
            .FinishSapDestination();
    }

    private static IHttpClientBuilder AddSapDestinationCore(
        this IHttpClientBuilder builder,
        Func<CancellationToken, Task<IHttpDestinationProperties>> resolve) =>
        builder.AddHttpMessageHandler(_ =>
            new DestinationRequestHandler(async cancellationToken => await resolve(cancellationToken).ConfigureAwait(false), PlaceholderBaseAddress))
            .FinishSapDestination();

    private static IHttpClientBuilder FinishSapDestination(this IHttpClientBuilder builder)
    {
        var factory = DefaultDestinationHttpClientFactory.Shared;
        return builder
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = PlaceholderBaseAddress;
                client.Timeout = factory.Timeout;
            })
            .AddHttpMessageHandler(_ => new CsrfTokenHandler())
            .ConfigurePrimaryHttpMessageHandler(_ => new DestinationPrimaryHandler(factory));
    }
}
