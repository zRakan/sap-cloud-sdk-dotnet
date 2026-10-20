using Sap.Cloud.Sdk.DataModel.OData.Client.Query;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>An <c>$orderby</c> expression: an ordered set of fields with their sort direction.</summary>
public sealed class OrderExpression
{
    private readonly OrderedDictionary<string, Order> _orderBy = [];

    private OrderExpression()
    {
    }

    /// <summary>Creates an ordering with a single term.</summary>
    /// <param name="fieldName">The field.</param>
    /// <param name="order">The direction.</param>
    /// <returns>The expression.</returns>
    public static OrderExpression Of(string fieldName, Order order) => new OrderExpression().And(fieldName, order);

    /// <summary>Serializes the ordering, for example <c>Name asc,Age desc</c>.</summary>
    /// <returns>The <c>$orderby</c> value.</returns>
    public string ToOrderByString() =>
        string.Join(",", _orderBy.Select(entry => entry.Key + " " + entry.Value.ToString().ToLowerInvariant()));

    /// <summary>Adds (or replaces) an ordering term.</summary>
    /// <param name="fieldName">The field.</param>
    /// <param name="order">The direction.</param>
    /// <returns>This expression.</returns>
    public OrderExpression And(string fieldName, Order order)
    {
        ArgumentNullException.ThrowIfNull(fieldName);
        _orderBy[fieldName] = order;
        return this;
    }
}
