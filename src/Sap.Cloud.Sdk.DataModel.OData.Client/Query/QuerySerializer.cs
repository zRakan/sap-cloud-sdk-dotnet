using Sap.Cloud.Sdk.DataModel.OData.Client.Request;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Query;

internal static class QuerySerializer
{
    private const string SeparatorSubQuery = ";";
    private const string SeparatorRootQuery = "&";

    internal static string SerializeAndEncodeQuery(StructuredQuery query, bool applyEncoding)
    {
        var parameters = new List<string>();
        Add(parameters, "$select", SelectorsToQueryString(query, applyEncoding));
        Add(parameters, "$expand", ExpansionsToQueryString(query, applyEncoding));
        Add(parameters, "$filter", FiltersToQueryString(query, applyEncoding));
        Add(parameters, "$top", query.TopValue?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add(parameters, "$skip", query.SkipValue?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Add(parameters, "$orderby", OrderByToQueryString(query, applyEncoding));
        Add(parameters, "$search", ConditionalEncode(query.SearchValue, applyEncoding));

        foreach (var (key, value) in query.CustomParameters)
        {
            if (query.Protocol.AllowCustomQueryParameter(query.IsRoot, key))
            {
                parameters.Add(key + "=" + ConditionalEncode(value, applyEncoding));
            }
        }
        var separator = query.IsRoot ? SeparatorRootQuery : SeparatorSubQuery;
        return string.Join(separator, parameters);
    }

    private static void Add(List<string> parameters, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            parameters.Add(name + "=" + value);
        }
    }

    private static string SelectorsToQueryString(StructuredQuery q, bool applyEncoding) =>
        string.Join(",", GetSelectors(q, applyEncoding));

    private static List<string> GetSelectors(StructuredQuery query, bool applyEncoding)
    {
        var selectors = query.SimpleSelectors
            .Select(result => applyEncoding ? ODataUriFactory.EncodeQuery(result) : result)
            .ToList();
        if (query.Protocol.IsEqualTo(ODataProtocol.V2))
        {
            foreach (var childQuery in query.ComplexSelectors)
            {
                var propertyName = childQuery.EntityOrPropertyName;
                if (applyEncoding)
                {
                    propertyName = ODataUriFactory.EncodeQuery(propertyName);
                }
                foreach (var select in GetSelectors(childQuery, applyEncoding))
                {
                    selectors.Add(propertyName + "/" + select);
                }
            }
        }
        return selectors;
    }

    private static string ExpansionsToQueryString(StructuredQuery q, bool applyEncoding) =>
        string.Join(",", GetExpansions(q, applyEncoding));

    private static List<string> GetExpansions(StructuredQuery query, bool applyEncoding)
    {
        var expansions = new List<string>();
        foreach (var subQuery in query.ComplexSelectors)
        {
            var propertyName = subQuery.EntityOrPropertyName;
            if (applyEncoding)
            {
                propertyName = ODataUriFactory.EncodeQuery(propertyName);
            }
            if (query.Protocol.IsEqualTo(ODataProtocol.V2))
            {
                expansions.Add(propertyName);
                foreach (var expand in GetExpansions(subQuery, applyEncoding))
                {
                    expansions.Add(propertyName + "/" + expand);
                }
            }
            else
            {
                var subQueryString = applyEncoding ? subQuery.EncodedQueryString : subQuery.QueryString;
                if (subQueryString.Length > 0)
                {
                    propertyName += "(" + subQueryString + ")";
                }
                expansions.Add(propertyName);
            }
        }
        return expansions;
    }

    private static string FiltersToQueryString(StructuredQuery q, bool applyEncoding)
    {
        var filters = q.Filters.Select(filter => filter.GetExpression(q.Protocol));
        return ConditionalEncode(string.Join(" and ", filters), applyEncoding)!;
    }

    private static string? OrderByToQueryString(StructuredQuery q, bool applyEncoding) =>
        ConditionalEncode(q.OrderByExpression?.ToOrderByString(), applyEncoding);

    private static string? ConditionalEncode(string? input, bool applyEncoding) =>
        applyEncoding && input is not null ? ODataUriFactory.EncodeQuery(input) : input;
}
