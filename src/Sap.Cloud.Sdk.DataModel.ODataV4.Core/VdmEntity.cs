namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core;

/// <summary>
/// Base class of all generated entities.
/// </summary>
/// <remarks>
/// Port of <c>VdmEntity&lt;?&gt;</c>. Java's <c>Option&lt;String&gt; getVersionIdentifier()</c> is the nullable
/// <see cref="VersionIdentifier"/> property.
/// </remarks>
public abstract class VdmEntity : VdmObject
{
    /// <summary>The version identifier (ETag) of the entity, if known.</summary>
    public string? VersionIdentifier { get; set; }

    /// <summary>The name of the entity set, for example <c>People</c>.</summary>
    protected internal abstract string EntityCollection { get; }

    /// <summary>The default service path of the service the entity belongs to, if the generated code knows it.</summary>
    protected internal virtual string? DefaultServicePath => null;

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        base.Equals(obj) && string.Equals(VersionIdentifier, ((VdmEntity)obj!).VersionIdentifier, StringComparison.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() => base.GetHashCode();
}

/// <summary>Base class of the generated entity <typeparamref name="TEntity"/>.</summary>
/// <typeparam name="TEntity">The generated entity type itself.</typeparam>
public abstract class VdmEntity<TEntity> : VdmEntity
    where TEntity : VdmEntity<TEntity>
{
}

/// <summary>Base class of all generated complex types.</summary>
public abstract class VdmComplex : VdmObject
{
}

/// <summary>Base class of the generated complex type <typeparamref name="TComplex"/>.</summary>
/// <typeparam name="TComplex">The generated complex type itself.</typeparam>
public abstract class VdmComplex<TComplex> : VdmComplex
    where TComplex : VdmComplex<TComplex>
{
}
