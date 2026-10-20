using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.OData.Helper;

/// <summary>The result type of function imports that return nothing.</summary>
/// <remarks>Replaces Java's <c>Void</c> as type argument of the function helpers.</remarks>
public sealed class VoidResult
{
    private VoidResult()
    {
    }
}

/// <summary>Calls a function import. <c>GET</c> function imports are sent as query, <c>POST</c> function imports as action.</summary>
/// <typeparam name="TFluentHelper">The concrete helper type.</typeparam>
/// <typeparam name="TObject">The type of the returned value (or of the elements, if a collection is returned).</typeparam>
/// <typeparam name="TResult">The result of <c>ExecuteAsync</c>.</typeparam>
/// <remarks>
/// <para>
/// Port of <c>FluentHelperFunction</c>. The abstract <c>createRequest(URI)</c> (which only determined the HTTP method) is replaced by
/// <see cref="RequestMethod"/>, and <c>executeRequest</c> by <see cref="FluentHelperBasic{TFluentHelper, TEntity, TResult}.ExecuteCoreAsync"/>,
/// which generated helpers implement with <see cref="ExecuteSingleAsync"/> or <see cref="ExecuteMultipleAsync"/>.
/// The result of a function import is read from <c>d</c> or, if present, from <c>d.{FunctionName}</c>.
/// </para>
/// </remarks>
public abstract class FluentHelperFunction<TFluentHelper, TObject, TResult> : FluentHelperBasic<TFluentHelper, TObject, TResult>, IFluentHelperFunction
{
    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(FluentHelperFunction<,,>).FullName!);

    private bool _csrfTokenConfigured;
    private bool _csrfTokenEnabled;

    /// <summary>Initializes a new instance of the <see cref="FluentHelperFunction{TFluentHelper, TObject, TResult}"/> class.</summary>
    /// <param name="servicePath">The path of the OData service.</param>
    protected FluentHelperFunction(string servicePath)
        : base(servicePath, null)
    {
    }

    /// <summary>The parameters of the function import by name, in the order of the declaration.</summary>
    protected abstract IReadOnlyDictionary<string, object?> Parameters { get; }

    /// <summary>The name of the function import.</summary>
    protected abstract string FunctionName { get; }

    /// <summary>The HTTP method of the function import: <see cref="HttpMethod.Get"/> or <see cref="HttpMethod.Post"/>.</summary>
    protected abstract HttpMethod RequestMethod { get; }

    /// <inheritdoc />
    protected override bool CsrfTokenEnabled
    {
        get
        {
            if (_csrfTokenConfigured)
            {
                return _csrfTokenEnabled;
            }
            if (RequestMethod == HttpMethod.Get)
            {
                return false;
            }
            if (RequestMethod == HttpMethod.Post)
            {
                return true;
            }
            Log.LogWarning("Encountered unexpected HTTP request method for function import: {Method}", RequestMethod);
            return _csrfTokenEnabled;
        }
        set
        {
            _csrfTokenConfigured = true;
            _csrfTokenEnabled = value;
        }
    }

    /// <summary>Fetches a CSRF token before the request (the default for <c>POST</c> function imports).</summary>
    /// <returns>This helper.</returns>
    public TFluentHelper WithCsrfToken()
    {
        CsrfTokenEnabled = true;
        return This;
    }

    /// <summary>Does not fetch a CSRF token before the request (the default for <c>GET</c> function imports).</summary>
    /// <returns>This helper.</returns>
    public TFluentHelper WithoutCsrfToken()
    {
        CsrfTokenEnabled = false;
        return This;
    }

    /// <inheritdoc />
    public override ODataRequestGeneric ToRequest() => AddHeadersAndCustomParameters(InstantiateRequest());

    private ODataRequestGeneric InstantiateRequest()
    {
        var resourcePath = ODataResourcePath.Of(FunctionName);

        // Get and encode the function import parameters as query expressions.
        var encodedQuery = ODataFunctionParameters.Of(Parameters, ODataProtocol.V2).ToEncodedString();

        if (RequestMethod == HttpMethod.Get)
        {
            return new ODataRequestFunction(ServicePath, resourcePath, encodedQuery, ODataProtocol.V2);
        }
        if (RequestMethod == HttpMethod.Post)
        {
            return new ODataRequestAction(ServicePath, resourcePath, null, encodedQuery, ODataProtocol.V2);
        }
        throw new InvalidOperationException("Unexpected HTTP request method for function import: " + RequestMethod);
    }

    /// <summary>Executes the request and reads a single value.</summary>
    /// <param name="target">Where to execute the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The value; <see langword="null"/> for <see cref="VoidResult"/>.</returns>
    protected async Task<TObject?> ExecuteSingleAsync(ODataExecutionTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        var result = await ExecuteInternalAsync(target, cancellationToken).ConfigureAwait(false);
        if (typeof(TObject) == typeof(VoidResult))
        {
            return default;
        }

        var resultObject = await ODataResponseReader.ReadSingleAsync<TObject>(result, FunctionName, cancellationToken).ConfigureAwait(false);
        if (resultObject is VdmEntity entity)
        {
            // Use the version identifier from the header if present.
            if (result.VersionIdentifierFromHeader is { } versionIdentifier)
            {
                entity.VersionIdentifier = versionIdentifier;
            }
            entity.AttachToService(ServicePath, target);
        }
        return resultObject;
    }

    /// <summary>Executes the request and reads a collection of values.</summary>
    /// <param name="target">Where to execute the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The values.</returns>
    protected async Task<IReadOnlyList<TObject>> ExecuteMultipleAsync(ODataExecutionTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        var result = await ExecuteInternalAsync(target, cancellationToken).ConfigureAwait(false);
        var resultObjects = await ODataResponseReader.ReadListAsync<TObject>(result, FunctionName, cancellationToken).ConfigureAwait(false);
        foreach (var entity in resultObjects.OfType<VdmEntity>())
        {
            entity.AttachToService(ServicePath, target);
        }
        return resultObjects;
    }

    private async Task<ODataRequestResultGeneric> ExecuteInternalAsync(ODataExecutionTarget target, CancellationToken cancellationToken) =>
        (ODataRequestResultGeneric)await ((IODataRequestExecutable)ToRequest()).ExecuteAsync(target.GetHttpClient(), cancellationToken).ConfigureAwait(false);
}

/// <summary>A function import helper whose result is a single value.</summary>
/// <typeparam name="TFluentHelper">The concrete helper type.</typeparam>
/// <typeparam name="TObject">The type of the value.</typeparam>
/// <typeparam name="TResult">The result of <c>ExecuteAsync</c>.</typeparam>
/// <remarks>Port of <c>SingleValuedFluentHelperFunction</c>.</remarks>
public abstract class SingleValuedFluentHelperFunction<TFluentHelper, TObject, TResult>
    : FluentHelperFunction<TFluentHelper, TObject, TResult>, ISingleValuedFluentHelperFunction<TObject>
{
    /// <summary>Initializes a new instance of the <see cref="SingleValuedFluentHelperFunction{TFluentHelper, TObject, TResult}"/> class.</summary>
    /// <param name="servicePath">The path of the OData service.</param>
    protected SingleValuedFluentHelperFunction(string servicePath)
        : base(servicePath)
    {
    }
}

/// <summary>A function import helper whose result is a collection.</summary>
/// <typeparam name="TFluentHelper">The concrete helper type.</typeparam>
/// <typeparam name="TObject">The type of the elements.</typeparam>
/// <typeparam name="TResult">The result of <c>ExecuteAsync</c>.</typeparam>
/// <remarks>Port of <c>CollectionValuedFluentHelperFunction</c>.</remarks>
public abstract class CollectionValuedFluentHelperFunction<TFluentHelper, TObject, TResult>
    : FluentHelperFunction<TFluentHelper, TObject, TResult>, ICollectionValuedFluentHelperFunction<TObject>
{
    /// <summary>Initializes a new instance of the <see cref="CollectionValuedFluentHelperFunction{TFluentHelper, TObject, TResult}"/> class.</summary>
    /// <param name="servicePath">The path of the OData service.</param>
    protected CollectionValuedFluentHelperFunction(string servicePath)
        : base(servicePath)
    {
    }
}
