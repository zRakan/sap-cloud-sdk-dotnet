using Prefixes = System.Collections.Generic.IReadOnlyDictionary<string, System.Func<Sap.Cloud.Sdk.DataModel.OData.Client.Expression.IFieldReference, bool>>;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>A filter operand of the OData type <c>Guid</c>.</summary>
public interface IValueGuid : IOperandSingle, IFilterableComparisonAbsolute, IFilterableComparisonRelative
{
    /// <summary>The <c>null</c> literal of this type.</summary>
    static new IValueGuid Null { get; } = new ValueGuidImpl((_, _) => "null");

    /// <summary>Creates a GUID literal.</summary>
    /// <param name="v">The value.</param>
    /// <returns>The operand.</returns>
    static IValueGuid Literal(Guid v) => new ValueGuidImpl((protocol, _) => protocol.UuidSerializer(v));
}
