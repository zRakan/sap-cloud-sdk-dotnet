using System.Text.Json;
using Sap.Cloud.Sdk.DataModel.OData.Adapter;
using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;
using Sap.Cloud.Sdk.DataModel.OData.Helper.Batch;

namespace Sap.Cloud.Sdk.DataModel.OData.Helper;

/// <summary>Base class of the helpers that modify data: create, update and delete.</summary>
/// <typeparam name="TFluentHelper">The concrete helper type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>Port of <c>FluentHelperModification</c>. A CSRF token is fetched by default.</remarks>
public abstract class FluentHelperModification<TFluentHelper, TEntity>
    : FluentHelperBasic<TFluentHelper, TEntity, ModificationResponse<TEntity>>, IFluentHelperModification
    where TEntity : VdmEntity
{
    /// <summary>Initializes a new instance of the <see cref="FluentHelperModification{TFluentHelper, TEntity}"/> class.</summary>
    /// <param name="servicePath">The path of the OData service.</param>
    /// <param name="entityCollection">The entity collection; if <see langword="null"/>, the collection of the entity is used.</param>
    protected FluentHelperModification(string servicePath, string? entityCollection)
        : base(servicePath, entityCollection)
    {
        CsrfTokenEnabled = true;
    }

    /// <summary>The entity to create, update or delete.</summary>
    protected abstract TEntity Entity { get; }

    /// <summary>Does not fetch a CSRF token.</summary>
    /// <returns>This helper.</returns>
    public TFluentHelper WithoutCsrfToken()
    {
        CsrfTokenEnabled = false;
        return This;
    }

    /// <summary>The entity collection from the helper or, if absent, from the entity.</summary>
    protected string ResolvedEntityCollection => EntityCollection ?? Entity.GetEntityCollection() ?? string.Empty;
}

/// <summary>Creates an entity (<c>POST</c>), optionally as child of another entity through a navigation property.</summary>
/// <typeparam name="TFluentHelper">The concrete helper type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>Port of <c>FluentHelperCreate</c>.</remarks>
public abstract class FluentHelperCreate<TFluentHelper, TEntity> : FluentHelperModification<TFluentHelper, TEntity>, IFluentHelperCreate<TEntity>, ICreatedEntityReader
    where TEntity : VdmEntity
{
    VdmEntity ICreatedEntityReader.ReadCreatedEntity(ODataRequestResultGeneric result) => ODataResponseReader.ReadSingleVdm<TEntity>(result, null);

    private string? _linkFromParentEntity;
    private VdmEntity? _parentEntity;

    /// <summary>Initializes a new instance of the <see cref="FluentHelperCreate{TFluentHelper, TEntity}"/> class.</summary>
    /// <param name="servicePath">The path of the OData service.</param>
    /// <param name="entityCollection">The entity collection.</param>
    protected FluentHelperCreate(string servicePath, string entityCollection)
        : base(servicePath, entityCollection)
    {
    }

    /// <inheritdoc />
    public override ODataRequestCreate ToRequest()
    {
        var entity = Entity;
        ODataResourcePath resourcePath;
        if (_linkFromParentEntity is not null && _parentEntity is not null)
        {
            resourcePath = ODataResourcePath
                .Of(_parentEntity.GetEntityCollection() ?? string.Empty, ODataEntityKey.Of(_parentEntity.GetKeyValues(), ODataProtocol.V2))
                .AddSegment(_linkFromParentEntity);
        }
        else
        {
            resourcePath = ODataResourcePath.Of(ResolvedEntityCollection);
        }

        string serializedEntity;
        try
        {
            serializedEntity = ODataEntitySerializer.SerializeEntityForCreate(entity);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or NotSupportedException)
        {
            throw new ODataSerializationException(
                new ODataRequestCreate(ServicePath, resourcePath, string.Empty, ODataProtocol.V2),
                entity,
                "Failed to serialize HTTP request entity of type " + entity.GetType().Name,
                e);
        }
        return AddHeadersAndCustomParameters(new ODataRequestCreate(ServicePath, resourcePath, serializedEntity, ODataProtocol.V2));
    }

    /// <summary>Creates the entity as child of another entity through one of its navigation properties.</summary>
    /// <typeparam name="TParent">The type of the parent entity.</typeparam>
    /// <typeparam name="TLink">The link type.</typeparam>
    /// <param name="entity">The parent entity; <see langword="null"/> creates the entity in its collection.</param>
    /// <param name="entityLink">The navigation property of the parent entity; <see langword="null"/> creates the entity in its collection.</param>
    /// <returns>This helper.</returns>
    public TFluentHelper AsChildOf<TParent, TLink>(TParent? entity, EntityLink<TLink, TParent, TEntity>? entityLink)
        where TParent : VdmEntity
        where TLink : EntityLink<TLink, TParent, TEntity>
    {
        _linkFromParentEntity = entityLink?.FieldName;
        _parentEntity = entity;
        return This;
    }

    /// <inheritdoc />
    protected override async Task<ModificationResponse<TEntity>> ExecuteCoreAsync(ODataExecutionTarget target, CancellationToken cancellationToken)
    {
        var result = await ToRequest().ExecuteAsync(target.GetHttpClient(), cancellationToken).ConfigureAwait(false);
        return ModificationResponse<TEntity>.Of(result, Entity, target);
    }
}

/// <summary>Updates an entity with <c>PATCH</c> (default) or <c>PUT</c>.</summary>
/// <typeparam name="TFluentHelper">The concrete helper type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>
/// Port of <c>FluentHelperUpdate</c>. By default only the fields changed since the entity was read (or since
/// <see cref="VdmObject.ResetChangedFields"/>) are sent; the version identifier of the entity is sent as <c>If-Match</c>.
/// </remarks>
public abstract class FluentHelperUpdate<TFluentHelper, TEntity> : FluentHelperModification<TFluentHelper, TEntity>, IFluentHelperUpdate<TEntity>
    where TEntity : VdmEntity
{
    private readonly List<IEntitySelectable<TEntity>> _includedFields = [];
    private readonly List<IEntitySelectable<TEntity>> _excludedFields = [];
    private UpdateStrategy _updateStrategy = UpdateStrategy.ModifyWithPatch;
    private ETagSubmissionStrategy _eTagSubmissionStrategy = ETagSubmissionStrategy.SubmitEtagFromEntity;

    /// <summary>Initializes a new instance of the <see cref="FluentHelperUpdate{TFluentHelper, TEntity}"/> class.</summary>
    /// <param name="servicePath">The path of the OData service.</param>
    /// <param name="entityCollection">The entity collection.</param>
    protected FluentHelperUpdate(string servicePath, string entityCollection)
        : base(servicePath, entityCollection)
    {
    }

    /// <summary>Does not send the version identifier of the entity.</summary>
    /// <returns>This helper.</returns>
    public TFluentHelper DisableVersionIdentifier()
    {
        _eTagSubmissionStrategy = ETagSubmissionStrategy.SubmitNoEtag;
        return This;
    }

    /// <summary>Sends <c>If-Match: *</c> instead of the version identifier of the entity.</summary>
    /// <returns>This helper.</returns>
    public TFluentHelper MatchAnyVersionIdentifier()
    {
        _eTagSubmissionStrategy = ETagSubmissionStrategy.SubmitAnyMatchEtag;
        return This;
    }

    /// <summary>Sends the given fields even if they have not been changed (for <c>PATCH</c>).</summary>
    /// <param name="fields">The fields.</param>
    /// <returns>This helper.</returns>
    public TFluentHelper IncludingFields(params IEntitySelectable<TEntity>[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        _includedFields.AddRange(fields);
        return This;
    }

    /// <summary>Does not send the given fields (for <c>PUT</c>).</summary>
    /// <param name="fields">The fields.</param>
    /// <returns>This helper.</returns>
    public TFluentHelper ExcludingFields(params IEntitySelectable<TEntity>[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        _excludedFields.AddRange(fields);
        return This;
    }

    /// <summary>Replaces the entity with <c>PUT</c>: all fields are sent.</summary>
    /// <returns>This helper.</returns>
    public TFluentHelper ReplacingEntity()
    {
        _updateStrategy = UpdateStrategy.ReplaceWithPut;
        return This;
    }

    /// <summary>Modifies the entity with <c>PATCH</c>: the changed fields are sent (the default).</summary>
    /// <returns>This helper.</returns>
    public TFluentHelper ModifyingEntity()
    {
        _updateStrategy = UpdateStrategy.ModifyWithPatch;
        return This;
    }

    /// <summary>Modifies the entity with <c>PATCH</c> using the given strategy for complex properties.</summary>
    /// <param name="strategy">The strategy.</param>
    /// <returns>This helper.</returns>
    public TFluentHelper ModifyingEntity(ModifyPatchStrategy strategy)
    {
        switch (strategy)
        {
            case ModifyPatchStrategy.Shallow:
                return ModifyingEntity();
            case ModifyPatchStrategy.RecursiveDelta:
                _updateStrategy = UpdateStrategy.ModifyWithPatchRecursiveDelta;
                break;
            case ModifyPatchStrategy.RecursiveFull:
                _updateStrategy = UpdateStrategy.ModifyWithPatchRecursiveFull;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown ModifyPatchStrategy: " + strategy);
        }
        return This;
    }

    /// <inheritdoc />
    public override ODataRequestUpdate ToRequest()
    {
        var entity = Entity;
        var serializedEntity = GetSerializedEntity();
        var versionIdentifier = _eTagSubmissionStrategy.GetHeaderFromVersionIdentifier(entity.VersionIdentifier);
        var request = new ODataRequestUpdate(
            ServicePath,
            ResolvedEntityCollection,
            ODataEntityKey.Of(entity.GetKeyValues(), ODataProtocol.V2),
            serializedEntity,
            _updateStrategy,
            versionIdentifier,
            ODataProtocol.V2);
        return AddHeadersAndCustomParameters(request);
    }

    private string GetSerializedEntity()
    {
        var entity = Entity;
        try
        {
            var fieldsToExclude = _excludedFields.Select(f => f.FieldName).ToList();
            var fieldsToInclude = _includedFields.Select(f => f.FieldName).ToList();
            return _updateStrategy switch
            {
                UpdateStrategy.ReplaceWithPut => ODataEntitySerializer.SerializeEntityForUpdatePut(entity, fieldsToExclude),
                UpdateStrategy.ModifyWithPatch => ODataEntitySerializer.SerializeEntityForUpdatePatchShallow(entity, fieldsToInclude),
                UpdateStrategy.ModifyWithPatchRecursiveDelta => ODataEntitySerializer.SerializeEntityForUpdatePatchRecursiveDelta(entity, fieldsToInclude),
                UpdateStrategy.ModifyWithPatchRecursiveFull => ODataEntitySerializer.SerializeEntityForUpdatePatchRecursiveFull(entity, fieldsToInclude),
                _ => throw new InvalidOperationException("Unexpected update strategy:" + _updateStrategy),
            };
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or NotSupportedException)
        {
            var request = new ODataRequestUpdate(
                ServicePath,
                entity.GetEntityCollection() ?? string.Empty,
                ODataEntityKey.Of(entity.GetKeyValues(), ODataProtocol.V2),
                string.Empty,
                _updateStrategy,
                _eTagSubmissionStrategy.GetHeaderFromVersionIdentifier(entity.VersionIdentifier),
                ODataProtocol.V2);
            throw new ODataSerializationException(
                request,
                entity,
                $"Failed to serialize OData Update HTTP request entity for type {entity.GetType().Name} with strategy {_updateStrategy}",
                e);
        }
    }

    /// <inheritdoc />
    protected override async Task<ModificationResponse<TEntity>> ExecuteCoreAsync(ODataExecutionTarget target, CancellationToken cancellationToken)
    {
        var result = await ToRequest().ExecuteAsync(target.GetHttpClient(), cancellationToken).ConfigureAwait(false);
        return ModificationResponse<TEntity>.Of(result, Entity, target);
    }
}

/// <summary>Deletes an entity (<c>DELETE</c>); the version identifier of the entity is sent as <c>If-Match</c>.</summary>
/// <typeparam name="TFluentHelper">The concrete helper type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>Port of <c>FluentHelperDelete</c>.</remarks>
public abstract class FluentHelperDelete<TFluentHelper, TEntity> : FluentHelperModification<TFluentHelper, TEntity>, IFluentHelperDelete<TEntity>
    where TEntity : VdmEntity
{
    private ETagSubmissionStrategy _eTagSubmissionStrategy = ETagSubmissionStrategy.SubmitEtagFromEntity;

    /// <summary>Initializes a new instance of the <see cref="FluentHelperDelete{TFluentHelper, TEntity}"/> class.</summary>
    /// <param name="servicePath">The path of the OData service.</param>
    /// <param name="entityCollection">The entity collection.</param>
    protected FluentHelperDelete(string servicePath, string entityCollection)
        : base(servicePath, entityCollection)
    {
    }

    /// <summary>Does not send the version identifier of the entity.</summary>
    /// <returns>This helper.</returns>
    public TFluentHelper DisableVersionIdentifier()
    {
        _eTagSubmissionStrategy = ETagSubmissionStrategy.SubmitNoEtag;
        return This;
    }

    /// <summary>Sends <c>If-Match: *</c> instead of the version identifier of the entity.</summary>
    /// <returns>This helper.</returns>
    public TFluentHelper MatchAnyVersionIdentifier()
    {
        _eTagSubmissionStrategy = ETagSubmissionStrategy.SubmitAnyMatchEtag;
        return This;
    }

    /// <inheritdoc />
    public override ODataRequestDelete ToRequest()
    {
        var entity = Entity;
        var versionIdentifier = _eTagSubmissionStrategy.GetHeaderFromVersionIdentifier(entity.VersionIdentifier);
        var request = new ODataRequestDelete(
            ServicePath,
            ResolvedEntityCollection,
            ODataEntityKey.Of(entity.GetKeyValues(), ODataProtocol.V2),
            versionIdentifier,
            ODataProtocol.V2);
        return AddHeadersAndCustomParameters(request);
    }

    /// <inheritdoc />
    protected override async Task<ModificationResponse<TEntity>> ExecuteCoreAsync(ODataExecutionTarget target, CancellationToken cancellationToken)
    {
        var result = await ToRequest().ExecuteAsync(target.GetHttpClient(), cancellationToken).ConfigureAwait(false);
        return ModificationResponse<TEntity>.Of(result, Entity, target);
    }
}
