using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>Something that can be executed against a service.</summary>
/// <typeparam name="TResult">The result type.</typeparam>
/// <remarks>
/// Port of <c>RequestBuilderExecutable</c>. Execution is asynchronous only. The destination overload obtains its
/// <see cref="HttpClient"/> from <see cref="ODataHttpClientProvider"/>.
/// </remarks>
public interface IRequestBuilderExecutable<TResult>
{
    /// <summary>Executes the request with the given client.</summary>
    /// <param name="httpClient">The client; its base address is the service host.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    Task<TResult> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default);

    /// <summary>Executes the request against a destination.</summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    Task<TResult> ExecuteAsync(IHttpDestinationProperties destination, CancellationToken cancellationToken = default);

    /// <summary>Executes the request against a destination and captures a failure.</summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result, or the failure (cancellation is never captured).</returns>
    async Task<Result<TResult>> TryExecuteAsync(IHttpDestinationProperties destination, CancellationToken cancellationToken = default)
    {
        try
        {
            return Result<TResult>.Success(await ExecuteAsync(destination, cancellationToken).ConfigureAwait(false));
        }
#pragma warning disable CA1031 // The failure is returned to the caller as a Result, as Try does in Java.
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return Result<TResult>.Failure(e);
        }
#pragma warning restore CA1031
    }
}

/// <summary>A request that can be inspected before it is executed (the result type is not known).</summary>
public interface IRequestBuilder
{
    /// <summary>Creates the protocol-level request.</summary>
    /// <returns>The request.</returns>
    ODataRequestGeneric ToRequest();
}

/// <summary>A read request of any result type; it can be added to a batch.</summary>
public interface IReadRequestBuilder : IRequestBuilder
{
}

/// <summary>A modifying request of any result type; it can be added to a change set of a batch.</summary>
public interface IModificationRequestBuilder : IRequestBuilder
{
}

/// <summary>A request that can be inspected and customized before it is executed.</summary>
/// <typeparam name="TResult">The result type.</typeparam>
public interface IRequestBuilder<TResult> : IRequestBuilder, IRequestBuilderExecutable<TResult>
{

    /// <summary>Adds a header value.</summary>
    /// <param name="key">The header name.</param>
    /// <param name="value">The value; <see langword="null"/> is ignored.</param>
    /// <returns>This builder.</returns>
    IRequestBuilder<TResult> WithHeader(string key, string? value);

    /// <summary>Adds header values.</summary>
    /// <param name="headers">The headers.</param>
    /// <returns>This builder.</returns>
    IRequestBuilder<TResult> WithHeaders(IEnumerable<KeyValuePair<string, string>> headers);
}

/// <summary>A read request. A CSRF token is only fetched if requested.</summary>
/// <typeparam name="TResult">The result type.</typeparam>
public interface IReadRequestBuilder<TResult> : IReadRequestBuilder, IRequestBuilder<TResult>
{
    /// <summary>Fetches a CSRF token before the request is sent.</summary>
    /// <returns>This builder.</returns>
    IReadRequestBuilder<TResult> WithCsrfToken();
}

/// <summary>A request that modifies data. A CSRF token is fetched unless disabled.</summary>
/// <typeparam name="TResult">The result type.</typeparam>
public interface IModificationRequestBuilder<TResult> : IModificationRequestBuilder, IRequestBuilder<TResult>
{
    /// <summary>Does not fetch a CSRF token.</summary>
    /// <returns>This builder.</returns>
    IModificationRequestBuilder<TResult> WithoutCsrfToken();
}

/// <summary>Shared implementation of all request builders.</summary>
/// <typeparam name="TBuilder">The concrete builder type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
/// <remarks>Port of the package-private <c>AbstractRequestBuilder</c>.</remarks>
public abstract class RequestBuilderBase<TBuilder, TResult> : IRequestBuilder<TResult>
    where TBuilder : RequestBuilderBase<TBuilder, TResult>
{
    private readonly List<KeyValuePair<string, string>> _headers = [];
    private readonly Dictionary<string, string> _parametersForRequestOnly = [];
    private readonly List<IODataRequestListener> _listeners = [];

    private protected RequestBuilderBase(string servicePath, ODataResourcePath resourcePath)
    {
        ArgumentNullException.ThrowIfNull(servicePath);
        ArgumentNullException.ThrowIfNull(resourcePath);
        ServicePath = servicePath;
        ResourcePath = resourcePath;
    }

    /// <summary>The service path.</summary>
    protected string ServicePath { get; }

    /// <summary>The resource path.</summary>
    protected ODataResourcePath ResourcePath { get; }

    /// <summary>Whether a CSRF token is fetched: <see langword="null"/> keeps the default of the request type.</summary>
    private protected bool? CsrfTokenEnabled { get; set; }

    /// <summary>Gets this builder as its concrete type.</summary>
    protected TBuilder This => (TBuilder)this;

    /// <summary>Registers a listener that is called before and after the request.</summary>
    /// <param name="listener">The listener.</param>
    /// <returns>This builder.</returns>
    public TBuilder WithListener(IODataRequestListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        _listeners.Add(listener);
        return This;
    }

    /// <summary>Adds a header value. Repeated calls with the same name send several values.</summary>
    /// <param name="key">The header name.</param>
    /// <param name="value">The value; <see langword="null"/> is ignored.</param>
    /// <returns>This builder.</returns>
    public TBuilder WithHeader(string key, string? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (value is not null)
        {
            _headers.Add(new(key, value));
        }

        return This;
    }

    /// <summary>Adds header values.</summary>
    /// <param name="headers">The headers.</param>
    /// <returns>This builder.</returns>
    public TBuilder WithHeaders(IEnumerable<KeyValuePair<string, string>> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        foreach (var (key, value) in headers)
        {
            WithHeader(key, value);
        }

        return This;
    }

    /// <summary>Adds a query parameter that is only sent with the HTTP request and is not part of the OData query.</summary>
    /// <param name="key">The parameter name.</param>
    /// <param name="value">The value; it is percent-encoded.</param>
    /// <returns>This builder.</returns>
    public TBuilder WithQueryParameter(string key, string? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        _parametersForRequestOnly[key] = value ?? string.Empty;
        return This;
    }

    /// <inheritdoc />
    public abstract ODataRequestGeneric ToRequest();

    /// <inheritdoc />
    public abstract Task<TResult> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    public Task<TResult> ExecuteAsync(IHttpDestinationProperties destination, CancellationToken cancellationToken = default) =>
        ExecuteAsync(ODataHttpClientProvider.GetHttpClient(destination), cancellationToken);

    IRequestBuilder<TResult> IRequestBuilder<TResult>.WithHeader(string key, string? value) => WithHeader(key, value);

    IRequestBuilder<TResult> IRequestBuilder<TResult>.WithHeaders(IEnumerable<KeyValuePair<string, string>> headers) =>
        WithHeaders(headers);

    /// <summary>Copies headers, query parameters, listeners and CSRF settings to the request.</summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="request">The request.</param>
    /// <returns>The same request.</returns>
    private protected TRequest Configure<TRequest>(TRequest request)
        where TRequest : ODataRequestGeneric
    {
        foreach (var (key, value) in _headers)
        {
            request.AddHeader(key, value);
        }

        foreach (var (key, value) in _parametersForRequestOnly)
        {
            request.AddQueryParameter(key, ODataUriFactory.EncodeQuery(value));
        }

        foreach (var listener in _listeners)
        {
            request.AddListener(listener);
        }

        if (CsrfTokenEnabled is { } enabled)
        {
            request.UseCsrfTokenRetrieval(enabled);
        }

        return request;
    }
}

/// <summary>Request builder that is bound to the entity type it reads or modifies.</summary>
/// <typeparam name="TBuilder">The concrete builder type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public abstract class EntityRequestBuilderBase<TBuilder, TEntity, TResult> : RequestBuilderBase<TBuilder, TResult>
    where TBuilder : EntityRequestBuilderBase<TBuilder, TEntity, TResult>
    where TEntity : VdmEntity
{
    private protected EntityRequestBuilderBase(string servicePath, ODataResourcePath entityPath)
        : base(servicePath, entityPath)
    {
    }
}

/// <summary>Helpers shared by the entity based builders.</summary>
internal static class VdmEntityUtil
{
    internal static string GetEntityCollection<TEntity>()
        where TEntity : VdmEntity =>
        NewInstance<TEntity>().EntityCollection;

    internal static TEntity NewInstance<TEntity>()
        where TEntity : VdmEntity
    {
        try
        {
            return Activator.CreateInstance<TEntity>();
        }
        catch (MissingMethodException e)
        {
            throw new InvalidOperationException($"Failed to instantiate object of type {typeof(TEntity).Name}.", e);
        }
    }

    internal static ODataResourcePath OfEntity(VdmEntity entity) =>
        new ODataResourcePath().AddSegment(entity.EntityCollection, entity.GetKey());
}
