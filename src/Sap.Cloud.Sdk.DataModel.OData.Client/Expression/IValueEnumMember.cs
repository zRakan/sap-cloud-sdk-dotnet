using Prefixes = System.Collections.Generic.IReadOnlyDictionary<string, System.Func<Sap.Cloud.Sdk.DataModel.OData.Client.Expression.IFieldReference, bool>>;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>A filter operand of the OData type <c>Enum</c>.</summary>
public interface IValueEnumMember : IOperandSingle, IFilterableComparisonAbsolute
{
    /// <summary>The <c>null</c> literal of this type.</summary>
    static new IValueEnumMember Null { get; } = new ValueEnumMemberImpl((_, _) => "null");

    /// <summary>Creates an enum literal without type name.</summary>
    /// <param name="v">The member name.</param>
    /// <returns>The operand.</returns>
    static IValueEnumMember Literal(string v) => new ValueEnumMemberImpl((_, _) => "'" + v + "'");

    /// <summary>Creates an enum literal with type name.</summary>
    /// <param name="enumType">The qualified enum type name.</param>
    /// <param name="v">The member name.</param>
    /// <returns>The operand.</returns>
    static IValueEnumMember Literal(string enumType, string v) => new ValueEnumMemberImpl((_, _) => enumType + "'" + v + "'");
}
