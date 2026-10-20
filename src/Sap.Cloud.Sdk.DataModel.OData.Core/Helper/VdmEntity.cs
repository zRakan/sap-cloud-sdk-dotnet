using System.Collections;
using Sap.Cloud.Sdk.CloudPlatform.Connectivity;
using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.OData.Helper;

/// <summary>Base class of complex types of the typed OData V2 model.</summary>
/// <typeparam name="TObject">The complex type itself.</typeparam>
/// <remarks>Port of <c>VdmComplex</c>, including the typed custom field accessors of <c>VdmObject&lt;ObjectT&gt;</c>.</remarks>
public abstract class VdmComplex<TObject> : VdmObject
{
    /// <summary>Sets a custom field, converting the value if the field has a type converter.</summary>
    /// <typeparam name="TField">The type of the field.</typeparam>
    /// <param name="customField">The field.</param>
    /// <param name="value">The value.</param>
    public void SetCustomField<TField>(EntityField<TObject, TField> customField, TField? value)
    {
        ArgumentNullException.ThrowIfNull(customField);
        SetCustomField(customField.FieldName, customField.TypeConverter, value);
    }

    /// <summary>Whether the field exists as custom field.</summary>
    /// <param name="customField">The field.</param>
    /// <returns><see langword="true"/> if it exists.</returns>
    public bool HasCustomField(IEntitySelectable<TObject> customField)
    {
        ArgumentNullException.ThrowIfNull(customField);
        return HasCustomField(customField.FieldName);
    }

    /// <summary>Gets the value of a custom field, converting it if the field has a type converter.</summary>
    /// <typeparam name="TField">The type of the field.</typeparam>
    /// <param name="customField">The field.</param>
    /// <returns>The value.</returns>
    /// <exception cref="Exceptions.NoSuchEntityFieldException">There is no such custom field.</exception>
    public TField? GetCustomField<TField>(EntityField<TObject, TField> customField)
    {
        ArgumentNullException.ThrowIfNull(customField);
        return GetCustomField(customField.FieldName, customField.TypeConverter);
    }
}

/// <summary>Base class of entities of the typed OData V2 model, without the type parameter.</summary>
/// <remarks>
/// Port of <c>VdmEntity</c>. Entities returned by fluent helpers remember where they came from (service path and
/// <see cref="ODataExecutionTarget"/>) so that navigation properties can be loaded lazily.
/// </remarks>
public abstract class VdmEntity : VdmObject
{
    private string? _versionIdentifier;
    private ODataExecutionTarget? _fetchTarget;

    internal static VdmTypeMetadata BaseMetadata { get; } = new(
        typeof(VdmEntity),
        [
            new VdmProperty<VdmEntity, string>(
                "versionIdentifier",
                static e => e._versionIdentifier,
                static (e, v) => e._versionIdentifier = v,
                isKey: false,
                isNavigation: false,
                converter: null),
        ],
        static () => throw new InvalidOperationException("VdmEntity is abstract."));

    /// <summary>
    /// The version identifier (ETag) of the entity as last read from the service; sent in the <c>If-Match</c> header of updates and
    /// deletes. <see langword="null"/> if unknown.
    /// </summary>
    public string? VersionIdentifier
    {
        get => _versionIdentifier;
        set => _versionIdentifier = value;
    }

    /// <summary>The name of the entity collection (entity set) of the entity.</summary>
    protected abstract string? EntityCollection { get; }

    internal string? GetEntityCollection() => EntityCollection;

    /// <summary>The service path used for lazy loading if the entity has not been attached to a service path.</summary>
    protected virtual string? DefaultServicePath => null;

    /// <summary>The service path used to load navigation properties of the entity.</summary>
    public string? ServicePathForFetch { get; protected internal set; }

    /// <summary>The destination used to load navigation properties of the entity, if it has been attached to one.</summary>
    public IHttpDestinationProperties? DestinationForFetch => _fetchTarget?.Destination;

    /// <summary>The target (client or destination) used to load navigation properties of the entity.</summary>
    protected internal ODataExecutionTarget? FetchTarget => _fetchTarget;

    /// <summary>Attaches the entity and the entities of its loaded navigation properties to a service and destination for lazy loading.</summary>
    /// <param name="servicePath">The service path; if <see langword="null"/>, the current or default service path is kept.</param>
    /// <param name="destination">The destination; may be <see langword="null"/>, which leaves the entity detached.</param>
    public void AttachToService(string? servicePath, IHttpDestinationProperties? destination) =>
        AttachToService(servicePath, destination is null ? null : ODataExecutionTarget.ForDestination(destination));

    /// <summary>Attaches the entity and the entities of its loaded navigation properties to a service and HTTP client for lazy loading.</summary>
    /// <param name="servicePath">The service path; if <see langword="null"/>, the current or default service path is kept.</param>
    /// <param name="httpClient">The HTTP client.</param>
    public void AttachToService(string? servicePath, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        AttachToService(servicePath, ODataExecutionTarget.ForClient(httpClient));
    }

    /// <summary>Attaches the entity and the entities of its loaded navigation properties to a service and execution target.</summary>
    /// <param name="servicePath">The service path; if <see langword="null"/>, the current or default service path is kept.</param>
    /// <param name="target">The target; may be <see langword="null"/>.</param>
    public void AttachToService(string? servicePath, ODataExecutionTarget? target)
    {
        if (servicePath is not null)
        {
            ServicePathForFetch = servicePath;
        }
        else
        {
            ServicePathForFetch ??= DefaultServicePath;
        }
        _fetchTarget = target;

        foreach (var navigationProperty in ToMapOfNavigationProperties().Values)
        {
            switch (navigationProperty)
            {
                case VdmEntity entity:
                    entity.AttachToService(ServicePathForFetch, target);
                    break;
                case IEnumerable items:
                    foreach (var item in items.OfType<VdmEntity>())
                    {
                        item.AttachToService(ServicePathForFetch, target);
                    }
                    break;
            }
        }
    }

    /// <summary>Loads a to-many navigation property.</summary>
    /// <typeparam name="T">The type of the related entities.</typeparam>
    /// <param name="fieldName">The name of the navigation property.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The related entities, attached to the same service.</returns>
    /// <exception cref="ODataRequestException">The entity is not attached to a destination or client.</exception>
    protected async Task<IReadOnlyList<T>> FetchFieldAsListAsync<T>(string fieldName, CancellationToken cancellationToken = default)
        where T : VdmEntity
    {
        var (request, target) = CreateFetchRequest(fieldName);
        var response = await request.ExecuteAsync(target.GetHttpClient(), cancellationToken).ConfigureAwait(false);
        var entities = await ODataResponseReader.ReadListAsync<T>(response, null, cancellationToken).ConfigureAwait(false);
        foreach (var entity in entities)
        {
            entity.AttachToService(ServicePathForFetch, target);
        }
        return entities;
    }

    /// <summary>Loads a to-one navigation property.</summary>
    /// <typeparam name="T">The type of the related entity.</typeparam>
    /// <param name="fieldName">The name of the navigation property.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The related entity, attached to the same service.</returns>
    /// <exception cref="ODataRequestException">The entity is not attached to a destination or client.</exception>
    protected async Task<T> FetchFieldAsSingleAsync<T>(string fieldName, CancellationToken cancellationToken = default)
        where T : VdmEntity
    {
        var (request, target) = CreateFetchRequest(fieldName);
        var response = await request.ExecuteAsync(target.GetHttpClient(), cancellationToken).ConfigureAwait(false);
        var entity = await ODataResponseReader.ReadSingleAsync<T>(response, null, cancellationToken).ConfigureAwait(false);
        entity.AttachToService(ServicePathForFetch, target);
        return entity;
    }

    private (ODataRequestRead Request, ODataExecutionTarget Target) CreateFetchRequest(string fieldName)
    {
        var entityKey = ODataEntityKey.Of(GetKey(), ODataProtocol.V2);
        var path = ODataResourcePath.Of(EntityCollection ?? string.Empty, entityKey).AddSegment(fieldName);
        var request = new ODataRequestRead(ServicePathForFetch ?? DefaultServicePath ?? string.Empty, path, null, ODataProtocol.V2);
        if (_fetchTarget is null)
        {
            throw new ODataRequestException(
                request,
                "Failed to fetch related objects from field name "
                + fieldName
                + ": The entity was created locally without an assigned HttpDestination. This method is applicable only on entities which were retrieved or created using the OData VDM.",
                null);
        }
        return (request, _fetchTarget);
    }
}

/// <summary>Base class of entities of the typed OData V2 model.</summary>
/// <typeparam name="TEntity">The entity type itself.</typeparam>
/// <remarks>Port of <c>VdmEntity</c>, including the typed custom field accessors of <c>VdmObject&lt;ObjectT&gt;</c>.</remarks>
public abstract class VdmEntity<TEntity> : VdmEntity
{
    /// <summary>Sets a custom field, converting the value if the field has a type converter.</summary>
    /// <typeparam name="TField">The type of the field.</typeparam>
    /// <param name="customField">The field.</param>
    /// <param name="value">The value.</param>
    public void SetCustomField<TField>(EntityField<TEntity, TField> customField, TField? value)
    {
        ArgumentNullException.ThrowIfNull(customField);
        SetCustomField(customField.FieldName, customField.TypeConverter, value);
    }

    /// <summary>Whether the field exists as custom field.</summary>
    /// <param name="customField">The field.</param>
    /// <returns><see langword="true"/> if it exists.</returns>
    public bool HasCustomField(IEntitySelectable<TEntity> customField)
    {
        ArgumentNullException.ThrowIfNull(customField);
        return HasCustomField(customField.FieldName);
    }

    /// <summary>Gets the value of a custom field, converting it if the field has a type converter.</summary>
    /// <typeparam name="TField">The type of the field.</typeparam>
    /// <param name="customField">The field.</param>
    /// <returns>The value.</returns>
    /// <exception cref="Exceptions.NoSuchEntityFieldException">There is no such custom field.</exception>
    public TField? GetCustomField<TField>(EntityField<TEntity, TField> customField)
    {
        ArgumentNullException.ThrowIfNull(customField);
        return GetCustomField(customField.FieldName, customField.TypeConverter);
    }
}

/// <summary>Base class of entities that have a media stream (<c>m:HasStream</c>).</summary>
/// <typeparam name="TEntity">The entity type itself.</typeparam>
/// <remarks>Port of <c>VdmMediaEntity</c>.</remarks>
public abstract class VdmMediaEntity<TEntity> : VdmEntity<TEntity>
{
    /// <summary>Loads the media stream of the entity (<c>/$value</c>).</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The content stream; disposing it releases the connection.</returns>
    /// <exception cref="ODataRequestException">The entity is not attached to a destination or client.</exception>
    /// <exception cref="ODataResponseException">The service answered with an error or without content.</exception>
    /// <remarks>
    /// The request is sent with the HTTP client directly because <see cref="ODataHttpResponse"/> does not give access to binary
    /// content of a response.
    /// </remarks>
    public async Task<Stream> FetchMediaStreamAsync(CancellationToken cancellationToken = default)
    {
        var resource = ODataResourcePath.Of(EntityCollection ?? string.Empty, ODataEntityKey.Of(GetKey(), ODataProtocol.V2)).AddSegment("$value");
        var request = new ODataRequestReadByKey(ServicePathForFetch ?? DefaultServicePath ?? string.Empty, resource, null, ODataProtocol.V2);
        if (FetchTarget is null)
        {
            throw new ODataRequestException(
                request,
                "Failed to fetch media stream.",
                new InvalidOperationException(
                    "Unable to execute OData query. The entity was created locally without an assigned HttpDestination. This method is applicable only on entities which were retrieved or created using the OData VDM."));
        }
        var httpClient = FetchTarget.GetHttpClient();
        using var message = new HttpRequestMessage(HttpMethod.Get, ResolveUri(httpClient, request.GetRelativeUri()));
        foreach (var (name, values) in request.GetHeaders())
        {
            message.Headers.TryAddWithoutValidation(name, values);
        }
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException e)
        {
            throw new ODataConnectionException(request, message, "Connection could not be established.", e);
        }
        if (!response.IsSuccessStatusCode)
        {
            using (response)
            {
                var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                var headers = response.Headers.Concat(response.Content.Headers)
                    .SelectMany(h => h.Value.Select(v => new KeyValuePair<string, string>(h.Key, v)));
                throw new ODataResponseException(
                    request,
                    new ODataHttpResponse((int)response.StatusCode, response.ReasonPhrase, headers, content),
                    $"The HTTP response code ({(int)response.StatusCode}) indicates an error.",
                    null);
            }
        }
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return new ResponseStream(stream, response);
    }

    private static Uri ResolveUri(HttpClient httpClient, Uri relative)
    {
        if (relative.IsAbsoluteUri || httpClient.BaseAddress is null)
        {
            return relative;
        }
        var basePart = httpClient.BaseAddress.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return new Uri(basePart + relative.OriginalString, UriKind.Absolute);
    }

    /// <summary>A stream that also disposes the HTTP response.</summary>
    private sealed class ResponseStream(Stream inner, HttpResponseMessage response) : Stream
    {
        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => inner.Read(buffer);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                response.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
