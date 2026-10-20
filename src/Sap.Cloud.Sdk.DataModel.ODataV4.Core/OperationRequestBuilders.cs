#pragma warning disable CA1000 // Static factory methods on generic types mirror the Java static factories (Of, Asc, Desc, OfAsync).
#pragma warning disable CA1711 // Type names mirror the Java API (Collection suffix).

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Adapter;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>Shared implementation of function request builders (<c>GET</c>).</summary>
/// <typeparam name="TBuilder">The concrete builder type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
/// <remarks>Port of <c>FunctionRequestBuilder</c>.</remarks>
public abstract class FunctionRequestBuilder<TBuilder, TResult> : RequestBuilderBase<TBuilder, TResult>, IReadRequestBuilder<TResult>
    where TBuilder : FunctionRequestBuilder<TBuilder, TResult>
{
    private protected FunctionRequestBuilder(string servicePath, ODataResourcePath functionPath)
        : base(servicePath, functionPath)
    {
    }

    /// <inheritdoc />
    public override ODataRequestFunction ToRequest() => ToFunctionRequest();

    private protected ODataRequestFunction ToFunctionRequest() =>
        Configure(new ODataRequestFunction(ServicePath, ResourcePath, null, ODataProtocol.V4));

    /// <summary>Fetches a CSRF token before the request is sent.</summary>
    /// <returns>This builder.</returns>
    public TBuilder WithCsrfToken()
    {
        CsrfTokenEnabled = true;
        return This;
    }

    IReadRequestBuilder<TResult> IReadRequestBuilder<TResult>.WithCsrfToken() => WithCsrfToken();
}

/// <summary>Invokes a function that returns one value (a primitive, a complex value or an entity).</summary>
/// <typeparam name="TResult">The result type.</typeparam>
/// <remarks>Port of <c>SingleValueFunctionRequestBuilder</c>.</remarks>
public class SingleValueFunctionRequestBuilder<TResult> : FunctionRequestBuilder<SingleValueFunctionRequestBuilder<TResult>, TResult>
{
    /// <summary>Initializes a new instance of the <see cref="SingleValueFunctionRequestBuilder{TResult}"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="functionName">The name of the function.</param>
    /// <param name="parameters">The parameters.</param>
    public SingleValueFunctionRequestBuilder(string servicePath, string functionName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
        : this(servicePath, ODataResourcePath.Of(functionName, ODataFunctionParameters.Of(parameters ?? [], ODataProtocol.V4)))
    {
    }

    internal SingleValueFunctionRequestBuilder(string servicePath, ODataResourcePath functionPath)
        : base(servicePath, functionPath)
    {
    }

    /// <inheritdoc />
    public override async Task<TResult> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        var response = await ToFunctionRequest().ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);
        var result = await response.AsSingleAsync<TResult>(null, cancellationToken).ConfigureAwait(false);
        if (result is VdmEntity entity && response.VersionIdentifierFromHeader is { } versionIdentifier)
        {
            entity.VersionIdentifier = versionIdentifier;
        }

        return result;
    }
}

/// <summary>Invokes a function that returns a collection of values.</summary>
/// <typeparam name="TResult">The item type.</typeparam>
/// <remarks>Port of <c>CollectionValueFunctionRequestBuilder</c>.</remarks>
public class CollectionValueFunctionRequestBuilder<TResult> : FunctionRequestBuilder<CollectionValueFunctionRequestBuilder<TResult>, IReadOnlyList<TResult>>
{
    /// <summary>Initializes a new instance of the <see cref="CollectionValueFunctionRequestBuilder{TResult}"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="functionName">The name of the function.</param>
    /// <param name="parameters">The parameters.</param>
    public CollectionValueFunctionRequestBuilder(string servicePath, string functionName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
        : this(servicePath, ODataResourcePath.Of(functionName, ODataFunctionParameters.Of(parameters ?? [], ODataProtocol.V4)))
    {
    }

    internal CollectionValueFunctionRequestBuilder(string servicePath, ODataResourcePath functionPath)
        : base(servicePath, functionPath)
    {
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyList<TResult>> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        var response = await ToFunctionRequest().ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);
        return await response.AsListAsync<TResult>(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Shared implementation of action request builders (<c>POST</c>).</summary>
/// <typeparam name="TBuilder">The concrete builder type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
/// <remarks>Port of <c>ActionRequestBuilder</c>.</remarks>
public abstract class ActionRequestBuilder<TBuilder, TResult> : RequestBuilderBase<TBuilder, TResult>, IModificationRequestBuilder<TResult>
    where TBuilder : ActionRequestBuilder<TBuilder, TResult>
{
    private readonly IReadOnlyDictionary<string, object?> _parameters;

    private protected ActionRequestBuilder(string servicePath, ODataResourcePath actionPath, IEnumerable<KeyValuePair<string, object?>>? parameters)
        : base(servicePath, actionPath)
    {
        _parameters = new Dictionary<string, object?>(parameters ?? []);
        CsrfTokenEnabled = true;
    }

    /// <inheritdoc />
    public override ODataRequestAction ToRequest() => ToActionRequest();

    private protected ODataRequestAction ToActionRequest() =>
        Configure(new ODataRequestAction(ServicePath, ResourcePath, SerializeParameters(), ODataProtocol.V4));

    private string SerializeParameters()
    {
        var payload = new JsonObject();
        foreach (var (key, value) in _parameters)
        {
            payload[key] = Serialize(value);
        }

        return payload.ToJsonString(VdmJson.OptionsWithNulls);
    }

    private static JsonNode? Serialize(object? parameter) => parameter switch
    {
        null => null,
        VdmObject vdmObject => JsonNode.Parse(VdmJson.Serialize(vdmObject, includeNulls: true)),
        _ => JsonSerializer.SerializeToNode(parameter, parameter.GetType(), VdmJson.OptionsWithNulls),
    };

    /// <summary>Does not fetch a CSRF token.</summary>
    /// <returns>This builder.</returns>
    public TBuilder WithoutCsrfToken()
    {
        CsrfTokenEnabled = false;
        return This;
    }

    IModificationRequestBuilder<TResult> IModificationRequestBuilder<TResult>.WithoutCsrfToken() => WithoutCsrfToken();
}

/// <summary>Invokes an action that returns one value, or nothing.</summary>
/// <typeparam name="TResult">The result type; <see cref="VdmVoid"/> if the action returns nothing.</typeparam>
/// <remarks>Port of <c>SingleValueActionRequestBuilder</c>.</remarks>
public class SingleValueActionRequestBuilder<TResult> : ActionRequestBuilder<SingleValueActionRequestBuilder<TResult>, ActionResponseSingle<TResult>>
{
    /// <summary>Initializes a new instance of the <see cref="SingleValueActionRequestBuilder{TResult}"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="actionName">The name of the action.</param>
    /// <param name="parameters">The parameters; they are sent as JSON.</param>
    public SingleValueActionRequestBuilder(string servicePath, string actionName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
        : this(servicePath, ODataResourcePath.Of(actionName), parameters)
    {
    }

    internal SingleValueActionRequestBuilder(string servicePath, ODataResourcePath actionPath, IEnumerable<KeyValuePair<string, object?>>? parameters)
        : base(servicePath, actionPath, parameters)
    {
    }

    /// <inheritdoc />
    public override async Task<ActionResponseSingle<TResult>> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        var response = await ToActionRequest().ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);
        return await ActionResponseSingle<TResult>.OfAsync(response, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Invokes an action that returns a collection of values, or nothing.</summary>
/// <typeparam name="TResult">The item type; <see cref="VdmVoid"/> if the action returns nothing.</typeparam>
/// <remarks>Port of <c>CollectionValueActionRequestBuilder</c>.</remarks>
public class CollectionValueActionRequestBuilder<TResult> : ActionRequestBuilder<CollectionValueActionRequestBuilder<TResult>, ActionResponseCollection<TResult>>
{
    /// <summary>Initializes a new instance of the <see cref="CollectionValueActionRequestBuilder{TResult}"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="actionName">The name of the action.</param>
    /// <param name="parameters">The parameters; they are sent as JSON.</param>
    public CollectionValueActionRequestBuilder(string servicePath, string actionName, IEnumerable<KeyValuePair<string, object?>>? parameters = null)
        : this(servicePath, ODataResourcePath.Of(actionName), parameters)
    {
    }

    internal CollectionValueActionRequestBuilder(string servicePath, ODataResourcePath actionPath, IEnumerable<KeyValuePair<string, object?>>? parameters)
        : base(servicePath, actionPath, parameters)
    {
    }

    /// <inheritdoc />
    public override async Task<ActionResponseCollection<TResult>> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        var response = await ToActionRequest().ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);
        return await ActionResponseCollection<TResult>.OfAsync(response, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>The response of an action that returns one value.</summary>
/// <typeparam name="TResult">The result type.</typeparam>
/// <remarks>
/// Port of <c>ActionResponseSingle</c>. Java's lazy <c>Option&lt;ResultT&gt;</c> is evaluated up front by
/// <see cref="OfAsync"/>; <see cref="HasResponseResult"/> tells whether the response carried a result.
/// </remarks>
public sealed class ActionResponseSingle<TResult>
{
    private ActionResponseSingle(ODataRequestResultGeneric result, bool hasResult, TResult? responseResult)
    {
        Result = result;
        HasResponseResult = hasResult;
        ResponseResult = responseResult;
    }

    internal ODataRequestResultGeneric Result { get; }

    /// <summary>Whether the response carried a result that could be parsed.</summary>
    public bool HasResponseResult { get; }

    /// <summary>The result, if <see cref="HasResponseResult"/>.</summary>
    public TResult? ResponseResult { get; }

    /// <summary>The result type.</summary>
    public Type ActionResultClass => typeof(TResult);

    /// <summary>The HTTP status code of the response.</summary>
    public int ResponseStatusCode => Result.StatusCode;

    /// <summary>The response headers.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ResponseHeaders => Result.GetAllHeaderValues();

    /// <summary>Evaluates a response.</summary>
    /// <param name="result">The protocol-level result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response.</returns>
    public static async Task<ActionResponseSingle<TResult>> OfAsync(ODataRequestResultGeneric result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (typeof(TResult) == typeof(VdmVoid))
        {
            return new(result, false, default);
        }

        try
        {
            var value = await result.AsSingleAsync<TResult>(null, cancellationToken).ConfigureAwait(false);
            if (value is VdmEntity entity && result.VersionIdentifierFromHeader is { } versionIdentifier)
            {
                entity.VersionIdentifier = versionIdentifier;
            }

            return new(result, true, value);
        }
#pragma warning disable CA1031 // As in Java, a response without a parsable result is not an error.
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Sap.Cloud.Sdk.CloudPlatform.SdkLogging.CreateLogger(typeof(ActionResponseSingle<>).FullName!)
                .LogDebug(e, "Failed to parse entity from HTTP response.");
            return new(result, false, default);
        }
#pragma warning restore CA1031
    }
}

/// <summary>The response of an action that returns a collection of values.</summary>
/// <typeparam name="TResult">The item type.</typeparam>
/// <remarks>Port of <c>ActionResponseCollection</c>.</remarks>
public sealed class ActionResponseCollection<TResult>
{
    private ActionResponseCollection(ODataRequestResultGeneric result, IReadOnlyList<TResult>? responseResult)
    {
        Result = result;
        ResponseResult = responseResult;
    }

    internal ODataRequestResultGeneric Result { get; }

    /// <summary>The results, or <see langword="null"/> if the response carried none that could be parsed.</summary>
    public IReadOnlyList<TResult>? ResponseResult { get; }

    /// <summary>The item type.</summary>
    public Type ActionResultClass => typeof(TResult);

    /// <summary>The HTTP status code of the response.</summary>
    public int ResponseStatusCode => Result.StatusCode;

    /// <summary>The response headers.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ResponseHeaders => Result.GetAllHeaderValues();

    /// <summary>Evaluates a response.</summary>
    /// <param name="result">The protocol-level result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response.</returns>
    public static async Task<ActionResponseCollection<TResult>> OfAsync(ODataRequestResultGeneric result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (typeof(TResult) == typeof(VdmVoid))
        {
            return new(result, null);
        }

        try
        {
            return new(result, await result.AsListAsync<TResult>(cancellationToken).ConfigureAwait(false));
        }
#pragma warning disable CA1031 // As in Java, a response without a parsable result is not an error.
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Sap.Cloud.Sdk.CloudPlatform.SdkLogging.CreateLogger(typeof(ActionResponseCollection<>).FullName!)
                .LogDebug(e, "Failed to parse entity from HTTP response.");
            return new(result, null);
        }
#pragma warning restore CA1031
    }
}
