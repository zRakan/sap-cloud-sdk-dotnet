#pragma warning disable CA1000 // Static factory methods on generic types mirror the Java static factories (Of, Asc, Desc, OfAsync).

using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;
using Sap.Cloud.Sdk.DataModel.OData.Client.Query;

namespace Sap.Cloud.Sdk.DataModel.ODataV4.Core.Expression;

/// <summary>Refers to the entity (or complex) type a field belongs to.</summary>
/// <remarks>
/// Port of <c>EntityReference&lt;EntityT&gt;</c>. <c>Class&lt;EntityT&gt;</c> is a <see cref="Type"/>; the generic parameter
/// keeps fields of different entities apart in the signatures of the query methods.
/// </remarks>
public interface IEntityReference
{
    /// <summary>The type the field belongs to.</summary>
    Type EntityType { get; }
}

/// <summary>Refers to the entity (or complex) type a field belongs to.</summary>
/// <typeparam name="TEntity">The type the field belongs to.</typeparam>
public interface IEntityReference<TEntity> : IEntityReference
{
}

/// <summary>The sort order of one field in <c>$orderby</c>.</summary>
public abstract class FieldOrdering
{
    internal FieldOrdering(IFieldReference property, Order order)
    {
        Property = property;
        Order = order;
    }

    /// <summary>The ordered field.</summary>
    public IFieldReference Property { get; }

    /// <summary>The direction.</summary>
    public Order Order { get; }

    /// <summary>Combines orderings to an <c>$orderby</c> expression.</summary>
    /// <param name="orderings">The orderings, most significant first.</param>
    /// <returns>The expression, or <see langword="null"/> if no ordering is given.</returns>
    public static OrderExpression? ToOrderExpression(params FieldOrdering[] orderings)
    {
        ArgumentNullException.ThrowIfNull(orderings);
        if (orderings.Length == 0)
        {
            return null;
        }

        var expression = OrderExpression.Of(orderings[0].Property.FieldName, orderings[0].Order);
        foreach (var ordering in orderings.Skip(1))
        {
            expression.And(ordering.Property.FieldName, ordering.Order);
        }

        return expression;
    }
}

/// <summary>The sort order of one field of <typeparamref name="TEntity"/> in <c>$orderby</c>.</summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public sealed class FieldOrdering<TEntity> : FieldOrdering
{
    /// <summary>Initializes a new instance of the <see cref="FieldOrdering{TEntity}"/> class.</summary>
    /// <param name="property">The field.</param>
    /// <param name="order">The direction.</param>
    public FieldOrdering(ISimpleProperty<TEntity> property, Order order)
        : base(property, order)
    {
    }

    /// <summary>Orders ascending.</summary>
    /// <param name="property">The field.</param>
    /// <returns>The ordering.</returns>
    public static FieldOrdering<TEntity> Asc(ISimpleProperty<TEntity> property) => new(property, Order.Asc);

    /// <summary>Orders descending.</summary>
    /// <param name="property">The field.</param>
    /// <returns>The ordering.</returns>
    public static FieldOrdering<TEntity> Desc(ISimpleProperty<TEntity> property) => new(property, Order.Desc);
}
