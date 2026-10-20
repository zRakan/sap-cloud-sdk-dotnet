#pragma warning disable CA1000 // Static factory methods on generic types mirror the Java static factories (Of, Asc, Desc, OfAsync).

using Microsoft.Extensions.Logging;
using Sap.Cloud.Sdk.CloudPlatform;
using Sap.Cloud.Sdk.DataModel.OData.Client.Request;
using Sap.Cloud.Sdk.DataModel.ODataV4.Core.Adapter;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>The response to a create, update or delete request.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>
/// Port of <c>ModificationResponse</c>. Java evaluates the response lazily on first access; reading the response
/// is asynchronous here, so <see cref="OfAsync"/> evaluates it up front and all members are plain properties.
/// </remarks>
public sealed class ModificationResponse<TEntity>
    where TEntity : VdmEntity
{
    private ModificationResponse(
        ODataRequestResultGeneric result,
        TEntity requestEntity,
        TEntity? responseEntity,
        TEntity modifiedEntity,
        string? updatedVersionIdentifier)
    {
        Result = result;
        RequestEntity = requestEntity;
        ResponseEntity = responseEntity;
        ModifiedEntity = modifiedEntity;
        UpdatedVersionIdentifier = updatedVersionIdentifier;
    }

    internal ODataRequestResultGeneric Result { get; }

    /// <summary>The entity that was sent.</summary>
    public TEntity RequestEntity { get; }

    /// <summary>The entity returned by the service, if the response carried one.</summary>
    public TEntity? ResponseEntity { get; }

    /// <summary>
    /// A copy of the entity returned by the service, or of the sent entity if the response had no payload, with the
    /// updated version identifier and without recorded changes.
    /// </summary>
    public TEntity ModifiedEntity { get; }

    /// <summary>The HTTP status code of the response.</summary>
    public int ResponseStatusCode => Result.StatusCode;

    /// <summary>The response headers.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ResponseHeaders => Result.GetAllHeaderValues();

    /// <summary>The version identifier (ETag) of the modified entity: the ETag header, else the one in the payload.</summary>
    public string? UpdatedVersionIdentifier { get; }

    /// <summary>Evaluates a response.</summary>
    /// <param name="result">The protocol-level result.</param>
    /// <param name="requestEntity">The entity that was sent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response.</returns>
    public static async Task<ModificationResponse<TEntity>> OfAsync(
        ODataRequestResultGeneric result,
        TEntity requestEntity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(requestEntity);
        var responseEntity = await ParseEntityFromResponseAsync(result, cancellationToken).ConfigureAwait(false);
        var updatedVersionIdentifier = result.VersionIdentifierFromHeader ?? responseEntity?.VersionIdentifier;
        var modifiedEntity = VdmJson.Copy(responseEntity ?? requestEntity);
        modifiedEntity.VersionIdentifier = updatedVersionIdentifier;
        return new(result, requestEntity, responseEntity, modifiedEntity, updatedVersionIdentifier);
    }

    private static async Task<TEntity?> ParseEntityFromResponseAsync(ODataRequestResultGeneric result, CancellationToken cancellationToken)
    {
        try
        {
            var entity = await result.AsSingleAsync<TEntity>(null, cancellationToken).ConfigureAwait(false);
            if (result.VersionIdentifierFromHeader is { } versionIdentifier)
            {
                entity.VersionIdentifier = versionIdentifier;
            }

            return entity;
        }
#pragma warning disable CA1031 // As in Java, a response without a parsable entity is not an error.
        catch (Exception e) when (e is not OperationCanceledException)
        {
            SdkLogging.CreateLogger<ModificationResponse<TEntity>>().LogDebug(e, "Failed to parse entity from HTTP response.");
            return null;
        }
#pragma warning restore CA1031
    }
}
