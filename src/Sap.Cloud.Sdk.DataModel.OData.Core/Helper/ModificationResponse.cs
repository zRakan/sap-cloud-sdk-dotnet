using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.DataModel.OData.Adapter;
using Sap.Cloud.Sdk.DataModel.OData.Client.Exceptions;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.OData.Helper;

/// <summary>The outcome of a create, update or delete request.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>
/// Port of <c>ModificationResponse</c>. The response entity and the modified entity are evaluated on first access, like in Java;
/// <see cref="ModifiedEntity"/> is a copy of the entity the service returned (or, if it returned none, of the request entity) with
/// the updated version identifier, attached to the service for lazy loading.
/// </remarks>
public sealed class ModificationResponse<TEntity>
    where TEntity : VdmEntity
{
    private static readonly ILogger Log = SdkLogging.CreateLogger<ModificationResponse<TEntity>>();

    private readonly Lock _lock = new();
    private readonly ODataRequestResultGeneric _result;
    private readonly ODataExecutionTarget _target;
    private TEntity? _modifiedEntity;
    private TEntity? _responseEntity;

    private ModificationResponse(ODataRequestResultGeneric result, TEntity originalRequestEntity, ODataExecutionTarget target)
    {
        _result = result;
        RequestEntity = originalRequestEntity;
        _target = target;
    }

    /// <summary>Creates a response object.</summary>
    /// <param name="result">The result of the request.</param>
    /// <param name="originalRequestEntity">The entity that was sent.</param>
    /// <param name="target">Where the request was executed.</param>
    /// <returns>The response object.</returns>
#pragma warning disable CA1000 // Mirrors the Java factory ModificationResponse.of.
    public static ModificationResponse<TEntity> Of(ODataRequestResultGeneric result, TEntity originalRequestEntity, ODataExecutionTarget target)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(originalRequestEntity);
        ArgumentNullException.ThrowIfNull(target);
        return new ModificationResponse<TEntity>(result, originalRequestEntity, target);
    }
#pragma warning restore CA1000

    /// <summary>The entity as modified by the request: the returned entity if the service returned one, otherwise the request entity, with the updated version identifier.</summary>
    public TEntity ModifiedEntity
    {
        get
        {
            lock (_lock)
            {
                EvaluateResponse();
                return _modifiedEntity!;
            }
        }
    }

    /// <summary>The entity the service returned in the response body, if any.</summary>
    public TEntity? ResponseEntity
    {
        get
        {
            lock (_lock)
            {
                EvaluateResponse();
                return _responseEntity;
            }
        }
    }

    /// <summary>The entity that was sent in the request.</summary>
    public TEntity RequestEntity { get; }

    /// <summary>The HTTP status code of the response.</summary>
    public int ResponseStatusCode => _result.StatusCode;

    /// <summary>The headers of the response.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ResponseHeaders => _result.GetAllHeaderValues();

    /// <summary>The version identifier after the modification: the <c>ETag</c> header of the response, else the version identifier of the returned entity.</summary>
    public string? UpdatedVersionIdentifier => _result.VersionIdentifierFromHeader ?? ResponseEntity?.VersionIdentifier;

    private void EvaluateResponse()
    {
        if (_modifiedEntity is not null)
        {
            return;
        }
        _responseEntity = ParseEntityFromResponse();

        // Create a copy before modifying the version identifier to not change existing objects.
        var entityToModify = _responseEntity ?? RequestEntity;
        var options = ODataVdmJsonOptions.Get();
        var runtimeType = RequestEntity.GetType();
        var clone = (TEntity)JsonSerializer.Deserialize(JsonSerializer.Serialize(entityToModify, entityToModify.GetType(), options), runtimeType, options)!;

        clone.AttachToService(_result.ODataRequest.ServicePath, _target);
        clone.VersionIdentifier = _result.VersionIdentifierFromHeader ?? _responseEntity?.VersionIdentifier;
        _modifiedEntity = clone;
    }

    private TEntity? ParseEntityFromResponse()
    {
        try
        {
            var entity = ODataResponseReader.ReadSingleVdm<TEntity>(_result, null);
            if (_result.VersionIdentifierFromHeader is { } versionIdentifier)
            {
                entity.VersionIdentifier = versionIdentifier;
            }
            return entity;
        }
        catch (ODataDeserializationException e)
        {
            Log.LogDebug(e, "Failed to parse entity from HTTP response.");
            return null;
        }
    }
}
