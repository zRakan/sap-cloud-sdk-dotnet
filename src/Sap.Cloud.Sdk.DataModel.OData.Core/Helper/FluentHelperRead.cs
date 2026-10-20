using System.Runtime.CompilerServices;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity;
using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.DataModel.OData.Client.Query;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.OData.Helper;

/// <summary>Reads the entities of an entity collection, with <c>$select</c>, <c>$expand</c>, <c>$filter</c>, <c>$orderby</c>, <c>$top</c> and <c>$skip</c>.</summary>
/// <typeparam name="TFluentHelper">The concrete helper type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TSelectable">The type of the selectable fields of the entity (the generated <c>I{Entity}Selectable</c>).</typeparam>
/// <remarks>
/// Port of <c>FluentHelperRead</c>. <c>ExecuteAsync</c> returns the entities of all pages. <c>iteratingPages</c> and
/// <c>iteratingEntities</c> return the pages or entities as <see cref="IAsyncEnumerable{T}"/> (the first page is requested when the
/// request is executed, further pages while enumerating); <c>streamingEntities</c> is covered by <see cref="IteratingEntities"/>.
/// </remarks>
public abstract class FluentHelperRead<TFluentHelper, TEntity, TSelectable>
    : FluentHelperBasic<TFluentHelper, TEntity, IReadOnlyList<TEntity>>, IFluentHelperRead<TEntity>
    where TEntity : VdmEntity
    where TSelectable : IEntitySelectable
{
    private readonly StructuredQuery _delegateQuery;

    /// <summary>Initializes a new instance of the <see cref="FluentHelperRead{TFluentHelper, TEntity, TSelectable}"/> class.</summary>
    /// <param name="servicePath">The path of the OData service.</param>
    /// <param name="entityCollection">The entity collection.</param>
    protected FluentHelperRead(string servicePath, string entityCollection)
        : base(servicePath, entityCollection)
    {
        ArgumentNullException.ThrowIfNull(entityCollection);
        _delegateQuery = StructuredQuery.OnEntity(entityCollection, ODataProtocol.V2);
    }

    /// <inheritdoc />
    public override ODataRequestRead ToRequest()
    {
        var request = new ODataRequestRead(ServicePath, EntityCollection!, _delegateQuery.EncodedQueryString, ODataProtocol.V2);
        return AddHeadersAndCustomParameters(request);
    }

    /// <summary>Adds a custom query parameter; the value is URL-encoded.</summary>
    /// <param name="key">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    /// <returns>This helper.</returns>
    public TFluentHelper WithQueryParameter(string key, string value) => AddQueryParameter(key, value);

    /// <summary>Selects fields and expands navigation properties.</summary>
    /// <param name="fields">The fields and links.</param>
    /// <returns>This helper.</returns>
    public TFluentHelper Select(params TSelectable[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        return ApplySelect(fields.Cast<IEntitySelectable>(), s => _delegateQuery.Select(s), q => _delegateQuery.Select(q));
    }

    /// <summary>Orders the result.</summary>
    /// <typeparam name="TField">The type of the field.</typeparam>
    /// <param name="field">The field to order by.</param>
    /// <param name="order">The sort order.</param>
    /// <returns>This helper.</returns>
    public TFluentHelper OrderBy<TField>(EntityField<TEntity, TField> field, Order order)
    {
        ArgumentNullException.ThrowIfNull(field);
        _delegateQuery.OrderBy(field.FieldName, order == Order.Asc ? Client.Query.Order.Asc : Client.Query.Order.Desc);
        return This;
    }

    /// <summary>Limits the number of returned entities (<c>$top</c>).</summary>
    /// <param name="top">The number of entities.</param>
    /// <returns>This helper.</returns>
    public TFluentHelper Top(long top)
    {
        _delegateQuery.Top(top);
        return This;
    }

    /// <summary>Skips entities (<c>$skip</c>).</summary>
    /// <param name="skip">The number of entities to skip.</param>
    /// <returns>This helper.</returns>
    public TFluentHelper Skip(long skip)
    {
        _delegateQuery.Skip(skip);
        return This;
    }

    /// <summary>Filters the result (<c>$filter</c>); further calls are combined with <c>and</c>.</summary>
    /// <param name="expression">The filter expression.</param>
    /// <returns>This helper.</returns>
    public TFluentHelper Filter(ExpressionFluentHelper<TEntity> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        _delegateQuery.Filter(expression.DelegateExpressionWithoutOuterParentheses);
        return This;
    }

    /// <summary>Creates a helper that counts the entities matching the filters of this helper.</summary>
    /// <returns>The count helper.</returns>
    public FluentHelperCount Count()
    {
        var prunedQuery = StructuredQuery.OnEntity(EntityCollection!, ODataProtocol.V2);
        prunedQuery.Filter([.. _delegateQuery.Filters]);
        foreach (var (key, value) in _delegateQuery.CustomParameters)
        {
            prunedQuery.WithCustomParameter(key, value);
        }
        var request = new ODataRequestCount(ServicePath, EntityCollection!, prunedQuery.EncodedQueryString, ODataProtocol.V2);
        return new FluentHelperCount(AddHeadersAndCustomParameters(request));
    }

    /// <summary>Asks the service to return pages of at most the given size (<c>Prefer: odata.maxpagesize</c>).</summary>
    /// <param name="size">The page size.</param>
    /// <returns>This helper.</returns>
    public TFluentHelper WithPreferredPageSize(int size) => WithHeader("Prefer", "odata.maxpagesize=" + size.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Fetches a CSRF token before reading.</summary>
    /// <returns>This helper.</returns>
    public TFluentHelper WithCsrfToken()
    {
        CsrfTokenEnabled = true;
        return This;
    }

    /// <summary>Gets an executable whose result is the pages of the result set; further pages are requested while enumerating.</summary>
    /// <returns>The executable.</returns>
    public IFluentHelperExecutable<IAsyncEnumerable<IReadOnlyList<TEntity>>> IteratingPages() => new DelegateExecutable<IAsyncEnumerable<IReadOnlyList<TEntity>>>(ExecuteInternalAsync);

    /// <summary>Gets an executable whose result is the entities of all pages; further pages are requested while enumerating.</summary>
    /// <returns>The executable.</returns>
    public IFluentHelperExecutable<IAsyncEnumerable<TEntity>> IteratingEntities() =>
        new DelegateExecutable<IAsyncEnumerable<TEntity>>(async (target, ct) =>
            Flatten(await ExecuteInternalAsync(target, ct).ConfigureAwait(false), ct));

    /// <inheritdoc />
    protected override async Task<IReadOnlyList<TEntity>> ExecuteCoreAsync(ODataExecutionTarget target, CancellationToken cancellationToken)
    {
        var entities = new List<TEntity>();
        await foreach (var page in (await ExecuteInternalAsync(target, cancellationToken).ConfigureAwait(false)).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            entities.AddRange(page);
        }
        return entities;
    }

    private static async IAsyncEnumerable<TEntity> Flatten(IAsyncEnumerable<IReadOnlyList<TEntity>> pages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var page in pages.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            foreach (var entity in page)
            {
                yield return entity;
            }
        }
    }

    private async Task<IAsyncEnumerable<IReadOnlyList<TEntity>>> ExecuteInternalAsync(ODataExecutionTarget target, CancellationToken cancellationToken)
    {
        var httpClient = target.GetHttpClient();
        var firstPage = await ToRequest().ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);
        return IteratePages(firstPage, httpClient, target, cancellationToken);
    }

    private async IAsyncEnumerable<IReadOnlyList<TEntity>> IteratePages(
        ODataRequestResultGeneric firstPage,
        HttpClient httpClient,
        ODataExecutionTarget target,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var page = firstPage;
        while (true)
        {
            var entities = await ODataResponseReader.ReadListAsync<TEntity>(page, null, cancellationToken).ConfigureAwait(false);
            foreach (var entity in entities)
            {
                // Enables lazy loading of navigation properties on the entity.
                entity.AttachToService(ServicePath, target);
            }
            yield return entities;
            if (await page.GetNextLinkAsync(cancellationToken).ConfigureAwait(false) is null)
            {
                yield break;
            }
            var next = await page.TryGetNextPageAsync(httpClient, cancellationToken).ConfigureAwait(false);
            if (next.IsFailure)
            {
                throw new ODataRequestException(page.ODataRequest, "Failed to handle next page.", next.Exception);
            }
            page = next.GetValueOrThrow();
        }
    }
}

/// <summary>Counts entities (<c>/$count</c>).</summary>
/// <remarks>Port of <c>FluentHelperCount</c>.</remarks>
public sealed class FluentHelperCount : IFluentHelperExecutable<long>, IFluentHelperRequest
{
    private readonly ODataRequestCount _request;

    internal FluentHelperCount(ODataRequestCount request)
    {
        _request = request;
    }

    /// <summary>Builds the count request.</summary>
    /// <returns>The request.</returns>
    public ODataRequestCount ToRequest() => _request;

    ODataRequestGeneric IFluentHelperRequest.ToRequest() => _request;

    /// <summary>Fetches a CSRF token before reading.</summary>
    /// <returns>This helper.</returns>
    public FluentHelperCount WithCsrfToken()
    {
        _request.UseCsrfTokenRetrieval(true);
        return this;
    }

    /// <inheritdoc />
    public Task<long> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        return ExecuteCoreAsync(ODataExecutionTarget.ForClient(httpClient), cancellationToken);
    }

    /// <inheritdoc />
    public Task<long> ExecuteAsync(IHttpDestinationProperties destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return ExecuteCoreAsync(ODataExecutionTarget.ForDestination(destination), cancellationToken);
    }

    private async Task<long> ExecuteCoreAsync(ODataExecutionTarget target, CancellationToken cancellationToken)
    {
        var result = await _request.ExecuteAsync(target.GetHttpClient(), cancellationToken).ConfigureAwait(false);
        return await result.AsSingleAsync<long>(null, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Reads one entity by its key.</summary>
/// <typeparam name="TFluentHelper">The concrete helper type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TSelectable">The type of the selectable fields of the entity.</typeparam>
/// <remarks>Port of <c>FluentHelperByKey</c>.</remarks>
public abstract class FluentHelperByKey<TFluentHelper, TEntity, TSelectable>
    : FluentHelperBasic<TFluentHelper, TEntity, TEntity>, IFluentHelperByKey<TEntity>
    where TEntity : VdmEntity
    where TSelectable : IEntitySelectable
{
    private readonly StructuredQuery _delegateQuery;

    /// <summary>Initializes a new instance of the <see cref="FluentHelperByKey{TFluentHelper, TEntity, TSelectable}"/> class.</summary>
    /// <param name="servicePath">The path of the OData service.</param>
    /// <param name="entityCollection">The entity collection.</param>
    protected FluentHelperByKey(string servicePath, string entityCollection)
        : base(servicePath, entityCollection)
    {
        ArgumentNullException.ThrowIfNull(entityCollection);
        _delegateQuery = StructuredQuery.OnEntity(entityCollection, ODataProtocol.V2);
    }

    /// <summary>The key of the entity, by OData property name.</summary>
    protected abstract IReadOnlyDictionary<string, object?> Key { get; }

    /// <inheritdoc />
    public override ODataRequestReadByKey ToRequest()
    {
        var entityKey = ODataEntityKey.Of(Key, ODataProtocol.V2);
        var request = new ODataRequestReadByKey(ServicePath, EntityCollection!, entityKey, _delegateQuery.EncodedQueryString, ODataProtocol.V2);
        return AddHeadersAndCustomParameters(request);
    }

    /// <summary>Adds a custom query parameter; the value is URL-encoded.</summary>
    /// <param name="key">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    /// <returns>This helper.</returns>
    public TFluentHelper WithQueryParameter(string key, string value) => AddQueryParameter(key, value);

    /// <summary>Selects fields and expands navigation properties.</summary>
    /// <param name="fields">The fields and links.</param>
    /// <returns>This helper.</returns>
    public TFluentHelper Select(params TSelectable[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        return ApplySelect(fields.Cast<IEntitySelectable>(), s => _delegateQuery.Select(s), q => _delegateQuery.Select(q));
    }

    /// <summary>Fetches a CSRF token before reading.</summary>
    /// <returns>This helper.</returns>
    public TFluentHelper WithCsrfToken()
    {
        CsrfTokenEnabled = true;
        return This;
    }

    /// <inheritdoc />
    protected override async Task<TEntity> ExecuteCoreAsync(ODataExecutionTarget target, CancellationToken cancellationToken)
    {
        var response = await ToRequest().ExecuteAsync(target.GetHttpClient(), cancellationToken).ConfigureAwait(false);
        var result = await ODataResponseReader.ReadSingleAsync<TEntity>(response, null, cancellationToken).ConfigureAwait(false);

        // Use the version identifier from the header if present.
        if (response.VersionIdentifierFromHeader is { } versionIdentifier)
        {
            result.VersionIdentifier = versionIdentifier;
        }
        result.AttachToService(ServicePath, target);
        return result;
    }
}
