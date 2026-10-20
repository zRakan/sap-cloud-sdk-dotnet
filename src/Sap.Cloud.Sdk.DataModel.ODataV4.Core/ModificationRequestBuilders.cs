using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Adapter;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>Creates an entity (<c>POST</c>), including entities that are nested in navigation properties (deep insert).</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>Port of <c>CreateRequestBuilder</c>.</remarks>
public class CreateRequestBuilder<TEntity> : EntityRequestBuilderBase<CreateRequestBuilder<TEntity>, TEntity, ModificationResponse<TEntity>>, IModificationRequestBuilder<ModificationResponse<TEntity>>
    where TEntity : VdmEntity
{
    private static readonly ILogger Log = SdkLogging.CreateLogger(typeof(CreateRequestBuilder<>).FullName!);

    /// <summary>Initializes a new instance of the <see cref="CreateRequestBuilder{TEntity}"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entity">The entity to create.</param>
    /// <param name="entityCollection">The name of the entity set.</param>
    public CreateRequestBuilder(string servicePath, TEntity entity, string entityCollection)
        : this(servicePath, ODataResourcePath.Of(entityCollection), entity)
    {
    }

    internal CreateRequestBuilder(string servicePath, ODataResourcePath entityPath, TEntity entity)
        : base(servicePath, entityPath)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Entity = entity;
        CsrfTokenEnabled = true;
    }

    /// <summary>The entity to create.</summary>
    public TEntity Entity { get; }

    /// <inheritdoc />
    public override ODataRequestCreate ToRequest() => ToCreateRequest();

    private ODataRequestCreate ToCreateRequest()
    {
        string serializedEntity;
        try
        {
            serializedEntity = VdmJson.Serialize(Entity);
        }
#pragma warning disable CA1031 // As in Java, every failure is reported as ODataSerializationException.
        catch (Exception e) when (e is not OperationCanceledException)
        {
            var message = "Failed to serialize HTTP request entity of type " + Entity.GetType().Name;
            if (Log.IsEnabled(LogLevel.Debug))
            {
                Log.LogDebug(e, "{Message}", message);
            }

            var request = new ODataRequestCreate(ServicePath, ResourcePath, string.Empty, ODataProtocol.V4);
            throw new ODataSerializationException(request, Entity, message, e);
        }
#pragma warning restore CA1031

        return Configure(new ODataRequestCreate(ServicePath, ResourcePath, serializedEntity, ODataProtocol.V4));
    }

    /// <inheritdoc />
    public override async Task<ModificationResponse<TEntity>> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        var response = await ToCreateRequest().ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);
        return await ModificationResponse<TEntity>.OfAsync(response, Entity, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Does not fetch a CSRF token.</summary>
    /// <returns>This builder.</returns>
    public CreateRequestBuilder<TEntity> WithoutCsrfToken()
    {
        CsrfTokenEnabled = false;
        return this;
    }

    IModificationRequestBuilder<ModificationResponse<TEntity>> IModificationRequestBuilder<ModificationResponse<TEntity>>.WithoutCsrfToken() =>
        WithoutCsrfToken();
}

/// <summary>Updates an entity with <c>PATCH</c> (changed fields only, the default) or <c>PUT</c> (the whole entity).</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>Port of <c>UpdateRequestBuilder</c>.</remarks>
public class UpdateRequestBuilder<TEntity> : EntityRequestBuilderBase<UpdateRequestBuilder<TEntity>, TEntity, ModificationResponse<TEntity>>, IModificationRequestBuilder<ModificationResponse<TEntity>>
    where TEntity : VdmEntity
{
    private readonly HashSet<IFieldReference> _includedFields = [];
    private readonly HashSet<IFieldReference> _excludedFields = [];
    private UpdateStrategy _updateStrategy = UpdateStrategy.ModifyWithPatch;
    private ETagSubmissionStrategy _eTagSubmissionStrategy = ETagSubmissionStrategy.SubmitEtagFromEntity;

    /// <summary>Initializes a new instance of the <see cref="UpdateRequestBuilder{TEntity}"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entity">The entity to update.</param>
    /// <param name="entityCollection">The name of the entity set.</param>
    public UpdateRequestBuilder(string servicePath, TEntity entity, string entityCollection)
        : this(servicePath, ODataResourcePath.Of(entityCollection, (entity ?? throw new ArgumentNullException(nameof(entity))).GetKey()), entity)
    {
    }

    internal UpdateRequestBuilder(string servicePath, ODataResourcePath entityPath, TEntity entity)
        : base(servicePath, entityPath)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Entity = entity;
        CsrfTokenEnabled = true;
    }

    /// <summary>The entity to update.</summary>
    public TEntity Entity { get; }

    /// <inheritdoc />
    public override ODataRequestUpdate ToRequest() => ToUpdateRequest();

    private ODataRequestUpdate ToUpdateRequest()
    {
        var versionIdentifier = _eTagSubmissionStrategy.GetHeaderFromVersionIdentifier(Entity.VersionIdentifier);
        var serializedEntity = GetSerializedEntity();
        return Configure(new ODataRequestUpdate(ServicePath, ResourcePath, serializedEntity, _updateStrategy, versionIdentifier, ODataProtocol.V4));
    }

    private string GetSerializedEntity()
    {
        try
        {
            return _updateStrategy switch
            {
                UpdateStrategy.ReplaceWithPut => UpdateRequestHelperPut.ToJson(Entity, _excludedFields),
                UpdateStrategy.ModifyWithPatch => UpdateRequestHelperPatch.ToJson(Entity, _includedFields),
                _ => throw new InvalidOperationException("Unexpected update Strategy: " + _updateStrategy),
            };
        }
#pragma warning disable CA1031 // As in Java, every failure is reported as ODataSerializationException.
        catch (Exception e) when (e is not OperationCanceledException)
        {
            var message = $"Failed to serialize OData Update HTTP request entity for type {Entity.GetType().Name} with strategy {_updateStrategy}";
            var request = new ODataRequestUpdate(
                ServicePath,
                ResourcePath,
                string.Empty,
                _updateStrategy,
                _eTagSubmissionStrategy.GetHeaderFromVersionIdentifier(Entity.VersionIdentifier),
                ODataProtocol.V4);
            throw new ODataSerializationException(request, Entity, message, e);
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public override async Task<ModificationResponse<TEntity>> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        var response = await ToUpdateRequest().ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);
        return await ModificationResponse<TEntity>.OfAsync(response, Entity, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends these fields with a PATCH request even if they were not changed.</summary>
    /// <param name="fields">The fields.</param>
    /// <returns>This builder.</returns>
    public UpdateRequestBuilder<TEntity> IncludingFields(params IFieldReference[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        _includedFields.UnionWith(fields);
        return this;
    }

    /// <summary>Leaves these fields out of a PUT request.</summary>
    /// <param name="fields">The fields.</param>
    /// <returns>This builder.</returns>
    public UpdateRequestBuilder<TEntity> ExcludingFields(params IFieldReference[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        _excludedFields.UnionWith(fields);
        return this;
    }

    /// <summary>Replaces the whole entity with <c>PUT</c>.</summary>
    /// <returns>This builder.</returns>
    public UpdateRequestBuilder<TEntity> ReplacingEntity()
    {
        _updateStrategy = UpdateStrategy.ReplaceWithPut;
        return this;
    }

    /// <summary>Modifies the changed fields with <c>PATCH</c> (default).</summary>
    /// <returns>This builder.</returns>
    public UpdateRequestBuilder<TEntity> ModifyingEntity()
    {
        _updateStrategy = UpdateStrategy.ModifyWithPatch;
        return this;
    }

    /// <summary>Does not send the version identifier of the entity in <c>If-Match</c>.</summary>
    /// <returns>This builder.</returns>
    public UpdateRequestBuilder<TEntity> DisableVersionIdentifier()
    {
        _eTagSubmissionStrategy = ETagSubmissionStrategy.SubmitNoEtag;
        return this;
    }

    /// <summary>Sends <c>If-Match: *</c>.</summary>
    /// <returns>This builder.</returns>
    public UpdateRequestBuilder<TEntity> MatchAnyVersionIdentifier()
    {
        _eTagSubmissionStrategy = ETagSubmissionStrategy.SubmitAnyMatchEtag;
        return this;
    }

    /// <summary>Does not fetch a CSRF token.</summary>
    /// <returns>This builder.</returns>
    public UpdateRequestBuilder<TEntity> WithoutCsrfToken()
    {
        CsrfTokenEnabled = false;
        return this;
    }

    IModificationRequestBuilder<ModificationResponse<TEntity>> IModificationRequestBuilder<ModificationResponse<TEntity>>.WithoutCsrfToken() =>
        WithoutCsrfToken();
}

/// <summary>Deletes an entity.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>Port of <c>DeleteRequestBuilder</c>.</remarks>
public class DeleteRequestBuilder<TEntity> : EntityRequestBuilderBase<DeleteRequestBuilder<TEntity>, TEntity, ModificationResponse<TEntity>>, IModificationRequestBuilder<ModificationResponse<TEntity>>
    where TEntity : VdmEntity
{
    private ETagSubmissionStrategy _eTagSubmissionStrategy = ETagSubmissionStrategy.SubmitEtagFromEntity;

    /// <summary>Initializes a new instance of the <see cref="DeleteRequestBuilder{TEntity}"/> class.</summary>
    /// <param name="servicePath">The service path.</param>
    /// <param name="entity">The entity to delete.</param>
    /// <param name="entityCollection">The name of the entity set.</param>
    public DeleteRequestBuilder(string servicePath, TEntity entity, string entityCollection)
        : this(servicePath, ODataResourcePath.Of(entityCollection, (entity ?? throw new ArgumentNullException(nameof(entity))).GetKey()), entity)
    {
    }

    internal DeleteRequestBuilder(string servicePath, ODataResourcePath entityPath, TEntity entity)
        : base(servicePath, entityPath)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Entity = entity;
        CsrfTokenEnabled = true;
    }

    /// <summary>The entity to delete.</summary>
    public TEntity Entity { get; }

    /// <inheritdoc />
    public override ODataRequestDelete ToRequest() => ToDeleteRequest();

    private ODataRequestDelete ToDeleteRequest()
    {
        var versionIdentifier = _eTagSubmissionStrategy.GetHeaderFromVersionIdentifier(Entity.VersionIdentifier);
        return Configure(new ODataRequestDelete(ServicePath, ResourcePath, versionIdentifier, ODataProtocol.V4));
    }

    /// <inheritdoc />
    public override async Task<ModificationResponse<TEntity>> ExecuteAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        var response = await ToDeleteRequest().ExecuteAsync(httpClient, cancellationToken).ConfigureAwait(false);
        return await ModificationResponse<TEntity>.OfAsync(response, Entity, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Does not send the version identifier of the entity in <c>If-Match</c>.</summary>
    /// <returns>This builder.</returns>
    public DeleteRequestBuilder<TEntity> DisableVersionIdentifier()
    {
        _eTagSubmissionStrategy = ETagSubmissionStrategy.SubmitNoEtag;
        return this;
    }

    /// <summary>Sends <c>If-Match: *</c>.</summary>
    /// <returns>This builder.</returns>
    public DeleteRequestBuilder<TEntity> MatchAnyVersionIdentifier()
    {
        _eTagSubmissionStrategy = ETagSubmissionStrategy.SubmitAnyMatchEtag;
        return this;
    }

    /// <summary>Does not fetch a CSRF token.</summary>
    /// <returns>This builder.</returns>
    public DeleteRequestBuilder<TEntity> WithoutCsrfToken()
    {
        CsrfTokenEnabled = false;
        return this;
    }

    IModificationRequestBuilder<ModificationResponse<TEntity>> IModificationRequestBuilder<ModificationResponse<TEntity>>.WithoutCsrfToken() =>
        WithoutCsrfToken();
}
