using System.Runtime.CompilerServices;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity;
using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Expression;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>Adapts a function to <see cref="IRequestBuilderExecutable{TResult}"/>.</summary>
internal sealed class DelegateExecutable<TResult>(Func<HttpClient, CancellationToken, Task<TResult>> execute) : IRequestBuilderExecutable<TResult>
{
    public Task<TResult> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default) =>
        execute(httpClient, cancellationToken);

    public Task<TResult> ExecuteAsync(IHttpDestinationProperties destination, CancellationToken cancellationToken = default) =>
        execute(ODataHttpClientProvider.GetHttpClient(destination), cancellationToken);
}

/// <summary>Reads all entities of an entity set (or navigation target), including all pages.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>
/// Port of <c>GetAllRequestBuilder</c>. The <c>Class&lt;EntityT&gt;</c> constructor argument is gone (the type argument
/// is enough). <c>iteratingPages</c>/<c>iteratingEntities</c>/<c>streamingEntities</c> become
/// <see cref="IteratingPages"/> and <see cref="IteratingEntities"/>, which produce an <see cref="IAsyncEnumerable{T}"/>
/// that requests further pages lazily; the first page is requested by <c>ExecuteAsync</c>.
/// </remarks>
public class GetAllRequestBuilder<TEntity> : EntityRequestBuilderBase<GetAllRequestBuilder<TEntity>, TEntity, IReadOnlyList<TEntity>>, IReadRequestBuilder<IReadOnlyList<TEntity>>
    where TEntity : VdmEntity
{
    private readonly NavigationPropertyCollectionQuery<TEntity, TEntity> _delegateQuery;

    /// <summary>Initializes a new instance of the <see cref="GetAllRequestBuilder{TEntity}"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entityCollection">The name of the entity set.</param>
    public GetAllRequestBuilder(string servicePath, string entityCollection)
        : this(servicePath, ODataResourcePath.Of(entityCollection))
    {
    }

    internal GetAllRequestBuilder(string servicePath, ODataResourcePath entityPath)
        : base(servicePath, entityPath)
    {
        _delegateQuery = NavigationPropertyCollectionQuery<TEntity, TEntity>.OfRootQuery(entityPath.ToString());
    }

    /// <inheritdoc />
    public override ODataRequestRead ToRequest() => ToReadRequest();

    private ODataRequestRead ToReadRequest() =>
        Configure(new ODataRequestRead(ServicePath, ResourcePath, _delegateQuery.EncodedQueryString, ODataProtocol.V4));

    /// <inheritdoc />
    public override async Task<IReadOnlyList<TEntity>> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        var entities = new List<TEntity>();
        await foreach (var page in IteratePagesCoreAsync(httpClient, cancellationToken).ConfigureAwait(false))
        {
            entities.AddRange(page);
        }

        return entities;
    }

    /// <summary>Requests the first page and then lazily the following pages.</summary>
    /// <returns>An executable that yields one list of entities per page.</returns>
    public IRequestBuilderExecutable<IAsyncEnumerable<IReadOnlyList<TEntity>>> IteratingPages() =>
        new DelegateExecutable<IAsyncEnumerable<IReadOnlyList<TEntity>>>(async (client, ct) =>
        {
            var result = await ToReadRequest().ExecuteAsync(client, ct).ConfigureAwait(false);
            return result.IteratePagesAsync<TEntity>(ct);
        });

    /// <summary>Requests the first page and then lazily the following pages, yielding single entities.</summary>
    /// <returns>An executable that yields the entities of all pages.</returns>
    public IRequestBuilderExecutable<IAsyncEnumerable<TEntity>> IteratingEntities() =>
        new DelegateExecutable<IAsyncEnumerable<TEntity>>(async (client, ct) =>
        {
            var pages = await IteratingPages().ExecuteAsync(client, ct).ConfigureAwait(false);
            return Flatten(pages, ct);
        });

    private static async IAsyncEnumerable<TEntity> Flatten(
        IAsyncEnumerable<IReadOnlyList<TEntity>> pages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var page in pages.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            foreach (var entity in page)
            {
                yield return entity;
            }
        }
    }

    private async IAsyncEnumerable<IReadOnlyList<TEntity>> IteratePagesCoreAsync(
        HttpClient httpClient,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var result = await ToReadRequest().ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);
        await foreach (var page in result.IteratePagesAsync<TEntity>(cancellationToken).ConfigureAwait(false))
        {
            yield return page;
        }
    }

    /// <summary>Sets the preferred page size (<c>Prefer: odata.maxpagesize</c>).</summary>
    /// <param name="size">The number of entities per page.</param>
    /// <returns>This builder.</returns>
    public GetAllRequestBuilder<TEntity> WithPreferredPageSize(int size) => WithHeader("Prefer", "odata.maxpagesize=" + size.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Selects properties (<c>$select</c>) and expands navigation properties (<c>$expand</c>).</summary>
    /// <param name="fields">The properties, or sub-queries on navigation or complex properties.</param>
    /// <returns>This builder.</returns>
    public GetAllRequestBuilder<TEntity> Select(params IProperty<TEntity>[] fields)
    {
        _delegateQuery.Select(fields);
        return this;
    }

    /// <summary>Filters the entities (<c>$filter</c>); several filters are combined with <c>and</c>.</summary>
    /// <param name="filters">The filters.</param>
    /// <returns>This builder.</returns>
    public GetAllRequestBuilder<TEntity> Filter(params IValueBoolean[] filters)
    {
        _delegateQuery.Filter(filters);
        return this;
    }

    /// <summary>Limits the number of entities (<c>$top</c>).</summary>
    /// <param name="top">The maximum number of entities.</param>
    /// <returns>This builder.</returns>
    public GetAllRequestBuilder<TEntity> Top(int top)
    {
        _delegateQuery.Top(top);
        return this;
    }

    /// <summary>Skips entities (<c>$skip</c>).</summary>
    /// <param name="skip">The number of entities to skip.</param>
    /// <returns>This builder.</returns>
    public GetAllRequestBuilder<TEntity> Skip(int skip)
    {
        _delegateQuery.Skip(skip);
        return this;
    }

    /// <summary>Orders the entities (<c>$orderby</c>).</summary>
    /// <param name="ordering">The orderings, most significant first.</param>
    /// <returns>This builder.</returns>
    public GetAllRequestBuilder<TEntity> OrderBy(params FieldOrdering<TEntity>[] ordering)
    {
        _delegateQuery.OrderBy(ordering);
        return this;
    }

    /// <summary>Searches for a literal text (<c>$search</c>).</summary>
    /// <param name="search">The text.</param>
    /// <returns>This builder.</returns>
    public GetAllRequestBuilder<TEntity> Search(string search)
    {
        _delegateQuery.Search(search);
        return this;
    }

    /// <summary>Searches for an expression (<c>$search</c>).</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>This builder.</returns>
    public GetAllRequestBuilder<TEntity> Search(SearchExpression expression)
    {
        _delegateQuery.Search(expression);
        return this;
    }

    /// <summary>Fetches a CSRF token before the request is sent.</summary>
    /// <returns>This builder.</returns>
    public GetAllRequestBuilder<TEntity> WithCsrfToken()
    {
        CsrfTokenEnabled = true;
        return this;
    }

    IReadRequestBuilder<IReadOnlyList<TEntity>> IReadRequestBuilder<IReadOnlyList<TEntity>>.WithCsrfToken() => WithCsrfToken();
}

/// <summary>Reads one entity by its key.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>Port of <c>GetByKeyRequestBuilder</c>. The version identifier is taken from the ETag response header.</remarks>
public class GetByKeyRequestBuilder<TEntity> : EntityRequestBuilderBase<GetByKeyRequestBuilder<TEntity>, TEntity, TEntity>, IReadRequestBuilder<TEntity>
    where TEntity : VdmEntity
{
    private readonly NavigationPropertySingleQuery<TEntity, TEntity> _delegateQuery;

    /// <summary>Initializes a new instance of the <see cref="GetByKeyRequestBuilder{TEntity}"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entityKey">The key properties and their values.</param>
    /// <param name="entityCollection">The name of the entity set.</param>
    public GetByKeyRequestBuilder(string servicePath, IEnumerable<KeyValuePair<string, object?>> entityKey, string entityCollection)
        : this(servicePath, ODataEntityKey.Of(entityKey, ODataProtocol.V4), entityCollection)
    {
    }

    internal GetByKeyRequestBuilder(string servicePath, ODataEntityKey entityKey, string entityCollection)
        : this(servicePath, ODataResourcePath.Of(entityCollection, entityKey))
    {
    }

    internal GetByKeyRequestBuilder(string servicePath, ODataResourcePath entityPath)
        : base(servicePath, entityPath)
    {
        _delegateQuery = NavigationPropertySingleQuery<TEntity, TEntity>.OfRootQuery(entityPath.ToString());
    }

    /// <inheritdoc />
    public override ODataRequestReadByKey ToRequest() => ToReadRequest();

    private ODataRequestReadByKey ToReadRequest() =>
        Configure(new ODataRequestReadByKey(ServicePath, ResourcePath, _delegateQuery.EncodedQueryString, ODataProtocol.V4));

    /// <inheritdoc />
    public override async Task<TEntity> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        var response = await ToReadRequest().ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);
        var entity = await response.AsSingleAsync<TEntity>(null, cancellationToken).ConfigureAwait(false);
        if (response.VersionIdentifierFromHeader is { } versionIdentifier)
        {
            entity.VersionIdentifier = versionIdentifier;
        }

        return entity;
    }

    /// <summary>Selects properties (<c>$select</c>) and expands navigation properties (<c>$expand</c>).</summary>
    /// <param name="fields">The properties, or sub-queries on navigation or complex properties.</param>
    /// <returns>This builder.</returns>
    public GetByKeyRequestBuilder<TEntity> Select(params IProperty<TEntity>[] fields)
    {
        _delegateQuery.Select(fields);
        return this;
    }

    /// <summary>Fetches a CSRF token before the request is sent.</summary>
    /// <returns>This builder.</returns>
    public GetByKeyRequestBuilder<TEntity> WithCsrfToken()
    {
        CsrfTokenEnabled = true;
        return this;
    }

    IReadRequestBuilder<TEntity> IReadRequestBuilder<TEntity>.WithCsrfToken() => WithCsrfToken();
}

/// <summary>Counts the entities of an entity set (<c>/$count</c>).</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>Port of <c>CountRequestBuilder</c>.</remarks>
public class CountRequestBuilder<TEntity> : EntityRequestBuilderBase<CountRequestBuilder<TEntity>, TEntity, long>, IReadRequestBuilder<long>
    where TEntity : VdmEntity
{
    private readonly NavigationPropertyCollectionQuery<TEntity, TEntity> _delegateQuery;

    /// <summary>Initializes a new instance of the <see cref="CountRequestBuilder{TEntity}"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entityCollection">The name of the entity set.</param>
    public CountRequestBuilder(string servicePath, string entityCollection)
        : this(servicePath, ODataResourcePath.Of(entityCollection))
    {
    }

    internal CountRequestBuilder(string servicePath, ODataResourcePath resourceToCount)
        : base(servicePath, resourceToCount)
    {
        _delegateQuery = NavigationPropertyCollectionQuery<TEntity, TEntity>.OfRootQuery(resourceToCount.ToString());
    }

    /// <inheritdoc />
    public override ODataRequestCount ToRequest() => ToCountRequest();

    private ODataRequestCount ToCountRequest() =>
        Configure(new ODataRequestCount(ServicePath, ResourcePath, _delegateQuery.EncodedQueryString, ODataProtocol.V4));

    /// <inheritdoc />
    public override async Task<long> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        var response = await ToCountRequest().ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);
        return await response.AsSingleAsync<long>(null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Filters the entities that are counted (<c>$filter</c>).</summary>
    /// <param name="filters">The filters.</param>
    /// <returns>This builder.</returns>
    public CountRequestBuilder<TEntity> Filter(params IValueBoolean[] filters)
    {
        _delegateQuery.Filter(filters);
        return this;
    }

    /// <summary>Searches for a literal text (<c>$search</c>).</summary>
    /// <param name="search">The text.</param>
    /// <returns>This builder.</returns>
    public CountRequestBuilder<TEntity> Search(string search)
    {
        _delegateQuery.Search(search);
        return this;
    }

    /// <summary>Searches for an expression (<c>$search</c>).</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>This builder.</returns>
    public CountRequestBuilder<TEntity> Search(SearchExpression expression)
    {
        _delegateQuery.Search(expression);
        return this;
    }

    /// <summary>Fetches a CSRF token before the request is sent.</summary>
    /// <returns>This builder.</returns>
    public CountRequestBuilder<TEntity> WithCsrfToken()
    {
        CsrfTokenEnabled = true;
        return this;
    }

    IReadRequestBuilder<long> IReadRequestBuilder<long>.WithCsrfToken() => WithCsrfToken();
}
