using Sap.Cloud.Sdk.DataModel.OData.Client;
using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

namespace Sap.Cloud.Sdk.DataModel.OData.Helper;

/// <summary>A navigation property that can be expanded.</summary>
public interface IEntityLink : IEntitySelectable
{
    /// <summary>The expand clauses this link contributes, for example <c>to_Parent</c> or <c>to_Children/to_Parent</c>.</summary>
    IReadOnlyList<string> Expansions { get; }
}

/// <summary>
/// A navigation property of an entity, usable to expand and select related entities and (for to-one links) to filter on them.
/// </summary>
/// <typeparam name="TLink">The link type that <see cref="Select"/> returns; generated code passes its own type.</typeparam>
/// <typeparam name="TEntity">The entity that owns the navigation property.</typeparam>
/// <typeparam name="TSubEntity">The related entity.</typeparam>
/// <remarks>
/// Port of <c>EntityLink</c>. Selecting from a link returns a new link (links are immutable); a generated link type overrides
/// <see cref="TranslateLinkType"/> to return its own type.
/// </remarks>
public class EntityLink<TLink, TEntity, TSubEntity> : IEntityLink, IEntitySelectable<TEntity>, IEquatable<EntityLink<TLink, TEntity, TSubEntity>>
    where TLink : EntityLink<TLink, TEntity, TSubEntity>
    where TEntity : VdmObject
    where TSubEntity : VdmObject
{
    private const string StarSelector = "*";

    private readonly List<IEntityLink> _descendants = [];
    private readonly List<IEntitySelectable<TSubEntity>> _selectors = [];

    /// <summary>Initializes a new instance of the <see cref="EntityLink{TLink, TEntity, TSubEntity}"/> class.</summary>
    /// <param name="fieldName">The name of the navigation property in the OData service.</param>
    public EntityLink(string fieldName)
    {
        ArgumentNullException.ThrowIfNull(fieldName);
        FieldName = fieldName;
    }

    /// <summary>Initializes a new instance as copy of another link.</summary>
    /// <param name="toCopy">The link to copy.</param>
    protected EntityLink(EntityLink<TLink, TEntity, TSubEntity> toCopy)
        : this(toCopy, null, null)
    {
    }

    private EntityLink(
        EntityLink<TLink, TEntity, TSubEntity> toCopy,
        IEnumerable<IEntityLink>? addDescendants,
        IEnumerable<IEntitySelectable<TSubEntity>>? addSelectors)
        : this(toCopy?.FieldName!)
    {
        _descendants.AddRange(toCopy!._descendants);
        _selectors.AddRange(toCopy._selectors);
        if (addDescendants is not null)
        {
            _descendants.AddRange(addDescendants);
        }
        if (addSelectors is not null)
        {
            _selectors.AddRange(addSelectors);
        }
    }

    /// <inheritdoc />
    public string FieldName { get; }

    /// <summary>Selects fields or navigation properties of the related entity.</summary>
    /// <param name="selectors">The fields (of the related entity) and links (to further entities).</param>
    /// <returns>A new link with the selection applied.</returns>
    public TLink Select(params IEntitySelectable<TSubEntity>[] selectors)
    {
        ArgumentNullException.ThrowIfNull(selectors);
        var additionalDescendants = new List<IEntityLink>();
        var additionalSelectors = new List<IEntitySelectable<TSubEntity>>();
        foreach (var select in selectors)
        {
            if (select is IEntityLink link)
            {
                additionalDescendants.Add(link);
            }
            else
            {
                additionalSelectors.Add(select);
            }
        }
        return TranslateLinkType(new EntityLink<TLink, TEntity, TSubEntity>(this, additionalDescendants, additionalSelectors));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Expansions
    {
        get
        {
            if (_descendants.Count == 0)
            {
                return [FieldName];
            }
            return _descendants.SelectMany(d => d.Expansions.Select(name => FieldName + "/" + name)).ToList();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Selections
    {
        get
        {
            var result = new List<string>();
            if (_selectors.Count == 0 && _descendants.Count == 0)
            {
                result.Add(FieldName + "/" + StarSelector);
            }
            result.AddRange(_selectors.Select(selector => FieldName + "/" + selector.FieldName));
            foreach (var descendant in _descendants)
            {
                result.AddRange(descendant.Selections.Select(name => FieldName + "/" + name));
            }
            return result;
        }
    }

    /// <summary>Converts a link created by <see cref="Select"/> to the link type of the generated code.</summary>
    /// <param name="link">The new link.</param>
    /// <returns>The link, as <typeparamref name="TLink"/>.</returns>
    protected virtual TLink TranslateLinkType(EntityLink<TLink, TEntity, TSubEntity> link) => (TLink)link;

    /// <summary>Creates a filter on the related entity of a to-one link.</summary>
    /// <param name="filterExpression">The filter on the related entity.</param>
    /// <returns>The filter on the owning entity.</returns>
    protected ExpressionFluentHelper<TEntity> FilterOnOneToOneLink(ExpressionFluentHelper<TSubEntity> filterExpression)
    {
        ArgumentNullException.ThrowIfNull(filterExpression);
        return new(new LinkFilter(FieldName, filterExpression.DelegateExpressionWithoutOuterParentheses));
    }

    /// <inheritdoc />
    public bool Equals(EntityLink<TLink, TEntity, TSubEntity>? other) =>
        other is not null
        && FieldName == other.FieldName
        && _descendants.SequenceEqual(other._descendants)
        && _selectors.SequenceEqual(other._selectors);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as EntityLink<TLink, TEntity, TSubEntity>);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(FieldName, _descendants.Count, _selectors.Count);

    /// <inheritdoc />
    public override string ToString() => $"{GetType().Name}(FieldName={FieldName}, Descendants=[{string.Join(", ", _descendants)}], Selectors=[{string.Join(", ", _selectors)}])";

    private sealed class LinkFilter(string linkName, IValueBoolean filter) : IValueBoolean
    {
        public string GetExpression(IODataProtocol protocol, IReadOnlyDictionary<string, Func<IFieldReference, bool>> prefixes) =>
            linkName + "/" + filter.GetExpression(protocol, prefixes);
    }
}
