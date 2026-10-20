using Sap.Cloud.Sdk.CloudPlatform.Connectivity;
using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Query;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.OData.Helper;

/// <summary>A fluent helper that can be turned into an OData request (for example to add it to a batch).</summary>
public interface IFluentHelperRequest
{
    /// <summary>Builds the OData request this helper would execute.</summary>
    /// <returns>The request.</returns>
    ODataRequestGeneric ToRequest();
}

/// <summary>Non-generic view of <see cref="FluentHelperRead{TFluentHelper, TEntity, TSelectable}"/>.</summary>
#pragma warning disable CA1040 // Marker interfaces let batch operations accept helpers of any entity type (Java wildcards).
public interface IFluentHelperRead : IFluentHelperRequest
{
}

/// <summary>View of a read helper with its entity type.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IFluentHelperRead<TEntity> : IFluentHelperRead
{
}

/// <summary>Non-generic view of <see cref="FluentHelperByKey{TFluentHelper, TEntity, TSelectable}"/>.</summary>
public interface IFluentHelperByKey : IFluentHelperRequest
{
}

/// <summary>View of a read-by-key helper with its entity type.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IFluentHelperByKey<TEntity> : IFluentHelperByKey
{
}

/// <summary>Non-generic view of <see cref="FluentHelperFunction{TFluentHelper, TObject, TResult}"/>.</summary>
public interface IFluentHelperFunction : IFluentHelperRequest
{
}

/// <summary>View of a function import helper that returns a single value.</summary>
/// <typeparam name="TObject">The result type.</typeparam>
public interface ISingleValuedFluentHelperFunction<TObject> : IFluentHelperFunction
{
}

/// <summary>View of a function import helper that returns a collection.</summary>
/// <typeparam name="TObject">The element type.</typeparam>
public interface ICollectionValuedFluentHelperFunction<TObject> : IFluentHelperFunction
{
}

/// <summary>Non-generic view of create, update and delete helpers.</summary>
public interface IFluentHelperModification : IFluentHelperRequest
{
}

/// <summary>View of a create helper with its entity type.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IFluentHelperCreate<TEntity> : IFluentHelperModification
{
}

/// <summary>View of an update helper with its entity type.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IFluentHelperUpdate<TEntity> : IFluentHelperModification
{
}

/// <summary>View of a delete helper with its entity type.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IFluentHelperDelete<TEntity> : IFluentHelperModification
{
}
#pragma warning restore CA1040

/// <summary>
/// Base class of all fluent helpers: collects headers and query parameters and executes the request it builds.
/// </summary>
/// <typeparam name="TFluentHelper">The concrete helper type, returned by the chaining methods.</typeparam>
/// <typeparam name="TEntity">The entity (or function result) type.</typeparam>
/// <typeparam name="TResult">The result of executing the request.</typeparam>
/// <remarks>
/// <para>
/// Port of <c>FluentHelperBasic</c>. Java's blocking <c>executeRequest(destination)</c> is replaced by the two asynchronous
/// <c>ExecuteAsync</c> overloads; they delegate to <see cref="ExecuteCoreAsync"/>. The abstract <c>getEntityClass()</c> is
/// not needed because the entity type is a type argument.
/// </para>
/// </remarks>
public abstract class FluentHelperBasic<TFluentHelper, TEntity, TResult> : IFluentHelperExecutable<TResult>, IFluentHelperRequest
{
    private readonly Dictionary<string, string> _headers = [];
    private readonly Dictionary<string, string> _parametersForRequestOnly = [];

    /// <summary>Initializes a new instance of the <see cref="FluentHelperBasic{TFluentHelper, TEntity, TResult}"/> class.</summary>
    /// <param name="servicePath">The path of the OData service.</param>
    /// <param name="entityCollection">The entity collection, if the helper works on one.</param>
    protected FluentHelperBasic(string servicePath, string? entityCollection)
    {
        ArgumentNullException.ThrowIfNull(servicePath);
        ServicePath = servicePath;
        EntityCollection = entityCollection;
    }

    /// <summary>The path of the OData service.</summary>
    protected string ServicePath { get; }

    /// <summary>The entity collection, if the helper works on one.</summary>
    protected string? EntityCollection { get; }

    /// <summary>Whether a CSRF token is fetched before the request is sent.</summary>
    protected virtual bool CsrfTokenEnabled { get; set; }

    /// <summary>The custom query parameters of the request (not part of the OData query options), by name.</summary>
    protected IReadOnlyDictionary<string, string> ParametersForRequestOnly => _parametersForRequestOnly;

    /// <summary>The headers added to the request.</summary>
    protected IReadOnlyDictionary<string, string> Headers => _headers;

    /// <summary>Gets this helper as the concrete type.</summary>
    protected TFluentHelper This => (TFluentHelper)(object)this;

    /// <summary>Adds a header to the request.</summary>
    /// <param name="key">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <returns>This helper.</returns>
    public TFluentHelper WithHeader(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        _headers[key] = value;
        return This;
    }

    /// <summary>Adds headers to the request.</summary>
    /// <param name="headers">The headers.</param>
    /// <returns>This helper.</returns>
    public TFluentHelper WithHeaders(IEnumerable<KeyValuePair<string, string>> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        foreach (var (key, value) in headers)
        {
            WithHeader(key, value);
        }
        return This;
    }

    /// <summary>Adds a custom query parameter; the value is URL-encoded.</summary>
    /// <param name="key">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    /// <returns>This helper.</returns>
    protected TFluentHelper AddQueryParameter(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        _parametersForRequestOnly[key] = value;
        return This;
    }

    /// <summary>Applies select and expand clauses from selectable fields and links.</summary>
    /// <param name="fields">The selected fields and links.</param>
    /// <param name="delegateSelect">Adds a select clause.</param>
    /// <param name="delegateExpand">Adds an expand clause.</param>
    /// <returns>This helper.</returns>
    protected TFluentHelper ApplySelect(IEnumerable<IEntitySelectable> fields, Action<string> delegateSelect, Action<StructuredQuery> delegateExpand)
    {
        foreach (var field in fields)
        {
            var selections = field.Selections;
            foreach (var selection in selections)
            {
                delegateSelect(selection);
            }
            foreach (var fieldName in selections)
            {
                var lastSlash = fieldName.LastIndexOf('/');
                if (lastSlash > 0)
                {
                    delegateExpand(StructuredQuery.AsNestedQueryOnProperty(fieldName[..lastSlash], ODataProtocol.V2));
                }
            }
        }
        return This;
    }

    /// <summary>Builds the OData request this helper would execute.</summary>
    /// <returns>The request.</returns>
    public abstract ODataRequestGeneric ToRequest();

    /// <summary>Adds the headers, custom query parameters and CSRF setting of this helper to a request.</summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="request">The request.</param>
    /// <returns>The request.</returns>
    protected TRequest AddHeadersAndCustomParameters<TRequest>(TRequest request)
        where TRequest : ODataRequestGeneric
    {
        ArgumentNullException.ThrowIfNull(request);
        foreach (var (key, value) in _headers)
        {
            request.AddHeader(key, value);
        }
        foreach (var (key, value) in _parametersForRequestOnly)
        {
            request.AddQueryParameter(key, ODataUriFactory.EncodeQuery(value));
        }
        request.UseCsrfTokenRetrieval(CsrfTokenEnabled);
        return request;
    }

    /// <summary>Executes the request.</summary>
    /// <param name="target">Where to execute the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    protected abstract Task<TResult> ExecuteCoreAsync(ODataExecutionTarget target, CancellationToken cancellationToken);

    /// <inheritdoc />
    public Task<TResult> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        return ExecuteCoreAsync(ODataExecutionTarget.ForClient(httpClient), cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResult> ExecuteAsync(IHttpDestinationProperties destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return ExecuteCoreAsync(ODataExecutionTarget.ForDestination(destination), cancellationToken);
    }
}

/// <summary>An <see cref="IFluentHelperExecutable{TResult}"/> defined by a function.</summary>
internal sealed class DelegateExecutable<TResult>(Func<ODataExecutionTarget, CancellationToken, Task<TResult>> execute) : IFluentHelperExecutable<TResult>
{
    public Task<TResult> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        return execute(ODataExecutionTarget.ForClient(httpClient), cancellationToken);
    }

    public Task<TResult> ExecuteAsync(IHttpDestinationProperties destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return execute(ODataExecutionTarget.ForDestination(destination), cancellationToken);
    }
}

