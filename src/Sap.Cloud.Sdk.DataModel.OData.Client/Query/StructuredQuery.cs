using Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Query;

/// <summary>The sort direction of an <c>$orderby</c> term.</summary>
public enum Order
{
    /// <summary>Ascending.</summary>
    Asc,

    /// <summary>Descending.</summary>
    Desc,
}

/// <summary>Something that can be serialized to an OData query string.</summary>
public interface IQuerySerializable
{
    /// <summary>The percent-encoded query string.</summary>
    string EncodedQueryString { get; }

    /// <summary>The query string without percent-encoding.</summary>
    string QueryString { get; }
}

/// <summary>A structured OData query: select, expand, filter, order, top, skip, search and custom parameters.</summary>
public sealed class StructuredQuery : IQuerySerializable
{
    private readonly List<string> _simpleSelectors = [];
    private readonly List<StructuredQuery> _complexSelectors = [];
    private readonly List<IValueBoolean> _filters = [];
    private readonly OrderedDictionary<string, string> _customParameters = [];

    private StructuredQuery(string entityOrPropertyName, bool isRoot, IODataProtocol protocol)
    {
        EntityOrPropertyName = entityOrPropertyName;
        IsRoot = isRoot;
        Protocol = protocol;
    }

    /// <summary>The name of the entity set or navigation property the query applies to.</summary>
    public string EntityOrPropertyName { get; }

    /// <summary>Whether this is the root query (not a nested query on a navigation property).</summary>
    public bool IsRoot { get; }

    /// <summary>The protocol.</summary>
    public IODataProtocol Protocol { get; }

    /// <summary>The selected simple properties.</summary>
    public IReadOnlyList<string> SimpleSelectors => _simpleSelectors;

    /// <summary>The selected/expanded navigation properties.</summary>
    public IReadOnlyList<StructuredQuery> ComplexSelectors => _complexSelectors;

    /// <summary>The filters, combined with <c>and</c>.</summary>
    public IReadOnlyList<IValueBoolean> Filters => _filters;

    /// <summary>The custom query parameters.</summary>
    public IReadOnlyDictionary<string, string> CustomParameters => _customParameters;

    /// <summary>The ordering.</summary>
    public OrderExpression? OrderByExpression { get; private set; }

    /// <summary>The <c>$top</c> value.</summary>
    public long? TopValue { get; private set; }

    /// <summary>The <c>$skip</c> value.</summary>
    public long? SkipValue { get; private set; }

    /// <summary>The <c>$search</c> value.</summary>
    public string? SearchValue { get; private set; }

    /// <inheritdoc />
    public string EncodedQueryString => QuerySerializer.SerializeAndEncodeQuery(this, true);

    /// <inheritdoc />
    public string QueryString => QuerySerializer.SerializeAndEncodeQuery(this, false);

    /// <summary>Creates a root query on an entity set.</summary>
    /// <param name="entityName">The entity set name.</param>
    /// <param name="protocol">The protocol.</param>
    /// <returns>The query.</returns>
    public static StructuredQuery OnEntity(string entityName, IODataProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(entityName);
        ArgumentNullException.ThrowIfNull(protocol);
        return new(entityName, true, protocol);
    }

    /// <summary>Creates a nested query on a navigation property.</summary>
    /// <param name="fieldName">The navigation property name.</param>
    /// <param name="protocol">The protocol.</param>
    /// <returns>The query.</returns>
    public static StructuredQuery AsNestedQueryOnProperty(string fieldName, IODataProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(fieldName);
        ArgumentNullException.ThrowIfNull(protocol);
        return new(fieldName, false, protocol);
    }

    /// <summary>Adds simple properties to <c>$select</c>.</summary>
    /// <param name="fields">The property names.</param>
    /// <returns>This query.</returns>
    public StructuredQuery Select(params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        foreach (var field in fields.Where(f => !_simpleSelectors.Contains(f)))
        {
            _simpleSelectors.Add(field);
        }
        return this;
    }

    /// <summary>Adds navigation properties to <c>$expand</c>; an existing sub-query for the same property is replaced.</summary>
    /// <param name="subqueries">The sub-queries.</param>
    /// <returns>This query.</returns>
    public StructuredQuery Select(params StructuredQuery[] subqueries)
    {
        ArgumentNullException.ThrowIfNull(subqueries);
        var names = subqueries.Select(q => q.EntityOrPropertyName).ToList();
        _complexSelectors.RemoveAll(s => names.Contains(s.EntityOrPropertyName));
        foreach (var query in subqueries.Where(q => !_complexSelectors.Contains(q)))
        {
            _complexSelectors.Add(query);
        }
        return this;
    }

    /// <summary>Adds filters; all filters are combined with <c>and</c>.</summary>
    /// <param name="filters">The filters.</param>
    /// <returns>This query.</returns>
    public StructuredQuery Filter(params IValueBoolean[] filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        _filters.AddRange(filters);
        return this;
    }

    /// <summary>Sets <c>$top</c>.</summary>
    /// <param name="top">The maximum number of results.</param>
    /// <returns>This query.</returns>
    public StructuredQuery Top(long? top)
    {
        TopValue = top;
        return this;
    }

    /// <summary>Sets <c>$skip</c>.</summary>
    /// <param name="skip">The number of results to skip.</param>
    /// <returns>This query.</returns>
    public StructuredQuery Skip(long? skip)
    {
        SkipValue = skip;
        return this;
    }

    /// <summary>Adds an ordering term.</summary>
    /// <param name="field">The field.</param>
    /// <param name="order">The direction.</param>
    /// <returns>This query.</returns>
    public StructuredQuery OrderBy(string field, Order order)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (OrderByExpression is null)
        {
            OrderByExpression = OrderExpression.Of(field, order);
        }
        else
        {
            OrderByExpression.And(field, order);
        }
        return this;
    }

    /// <summary>Replaces the ordering.</summary>
    /// <param name="ordering">The ordering.</param>
    /// <returns>This query.</returns>
    public StructuredQuery OrderBy(OrderExpression ordering)
    {
        OrderByExpression = ordering;
        return this;
    }

    /// <summary>Sets <c>$search</c>.</summary>
    /// <param name="search">The search expression.</param>
    /// <returns>This query.</returns>
    public StructuredQuery Search(string? search)
    {
        SearchValue = search;
        return this;
    }

    /// <summary>Adds a custom query parameter (root queries only).</summary>
    /// <param name="key">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    /// <returns>This query.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">The query is a nested query.</exception>
    public StructuredQuery WithCustomParameter(string key, string value)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Custom parameter key must not be null or empty.", nameof(key));
        }
        if (!IsRoot)
        {
            throw new InvalidOperationException(
                "Custom query parameters can only be added to the HTTP query but not to nested OData queries on navigation properties.");
        }
        _customParameters[key] = value;
        return this;
    }

    /// <summary>Requests the inline count.</summary>
    /// <returns>This query.</returns>
    public StructuredQuery WithInlineCount()
    {
        var queryOption = Protocol.GetQueryOptionInlineCount(true);
        _customParameters[queryOption.Key] = queryOption.Value;
        return this;
    }
}
