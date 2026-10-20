using Sap.Cloud.Sdk.CloudPlatform.Connectivity.Exceptions;

namespace Sap.Cloud.Sdk.CloudPlatform.Connectivity;

/// <summary>Retrieves a CSRF token from a remote system.</summary>
/// <remarks>
/// Port of <c>CsrfTokenRetriever</c>. Java's two overloads (with and without headers) are one async method with an
/// optional <c>headers</c> parameter.
/// </remarks>
public interface ICsrfTokenRetriever
{
    /// <summary>The disabled retriever: <see cref="RetrieveCsrfTokenAsync"/> throws <see cref="InvalidOperationException"/>.</summary>
    static ICsrfTokenRetriever Disabled { get; } = new DisabledCsrfTokenRetriever();

    /// <summary>Whether CSRF token retrieval is enabled.</summary>
    bool IsEnabled => true;

    /// <summary>Retrieves a CSRF token from a remote system by issuing a CSRF token request.</summary>
    /// <param name="httpClient">The client used to issue the request.</param>
    /// <param name="servicePath">The service path, appended to the destination URI by the client, used for the request.</param>
    /// <param name="headers">Additional headers for the request, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The fetched token.</returns>
    /// <exception cref="CsrfTokenRetrievalException">The token could not be fetched.</exception>
    Task<CsrfToken> RetrieveCsrfTokenAsync(
        HttpClient httpClient,
        string servicePath,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? headers = null,
        CancellationToken cancellationToken = default);

    private sealed class DisabledCsrfTokenRetriever : ICsrfTokenRetriever
    {
        public bool IsEnabled => false;

        public Task<CsrfToken> RetrieveCsrfTokenAsync(
            HttpClient httpClient,
            string servicePath,
            IReadOnlyDictionary<string, IReadOnlyCollection<string>>? headers = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The disabled CSRF token retriever cannot retrieve an CSRF token.");
    }
}
