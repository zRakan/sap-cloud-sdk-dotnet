using Prefixes = System.Collections.Generic.IReadOnlyDictionary<string, System.Func<Sap.Cloud.Sdk.DataModel.OData.Client.Expression.IFieldReference, bool>>;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>A filter operand of the OData type <c>Binary</c>.</summary>
public interface IValueBinary : IOperandSingle, IFilterableComparisonAbsolute
{
    /// <summary>The <c>null</c> literal of this type.</summary>
    static new IValueBinary Null { get; } = new ValueBinaryImpl((_, _) => "null");

    /// <summary>Creates a binary literal (Base64 encoded).</summary>
    /// <param name="v">The bytes.</param>
    /// <returns>The operand.</returns>
    static IValueBinary Literal(byte[] v)
    {
        ArgumentNullException.ThrowIfNull(v);
        var value = EncodeToString(v);
        return new ValueBinaryImpl((_, _) => "binary'" + value + "'");
    }

    /// <summary>Encodes bytes as Base64 string.</summary>
    /// <param name="v">The bytes.</param>
    /// <returns>The string.</returns>
    static string EncodeToString(byte[] v) => Convert.ToBase64String(v);

    /// <summary>Decodes a Base64 string; missing padding is tolerated.</summary>
    /// <param name="v">The string.</param>
    /// <returns>The bytes.</returns>
    static byte[] DecodeFromString(string v)
    {
        ArgumentNullException.ThrowIfNull(v);
        // Accept both the standard and the URL-safe alphabet, with or without padding (as the Java SDK does).
        var normalized = v.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(normalized.Length % 4 == 0
            ? normalized
            : normalized.PadRight(normalized.Length + 4 - (normalized.Length % 4), '='));
    }

    /// <summary>A filter expression that evaluates to a <c>Binary</c>.</summary>
    /// <param name="delegate">The underlying expression.</param>
    public sealed class Expression(IFilterExpression @delegate) : IFilterExpression, IValueBinary
    {
        /// <inheritdoc />
        public string FilterOperator => @delegate.FilterOperator;

        /// <inheritdoc />
        public IReadOnlyList<IOperand> Operands => @delegate.Operands;

        /// <summary>Renders the expression without lambda variable prefixes.</summary>
        /// <param name="protocol">The OData protocol.</param>
        /// <returns>The expression string.</returns>
        public string GetExpression(IODataProtocol protocol) => GetExpression(protocol, Expressions.NoPrefixes);

        /// <inheritdoc />
        public string GetExpression(IODataProtocol protocol, IReadOnlyDictionary<string, Func<IFieldReference, bool>> prefixes) =>
            @delegate.GetExpression(protocol, prefixes);
    }
}
