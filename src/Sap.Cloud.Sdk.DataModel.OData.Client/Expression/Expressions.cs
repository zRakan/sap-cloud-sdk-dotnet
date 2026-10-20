using System.Numerics;
using Prefixes = System.Collections.Generic.IReadOnlyDictionary<string, System.Func<Sap.Cloud.Sdk.DataModel.OData.Client.Expression.IFieldReference, bool>>;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

/// <summary>
/// An operand of a filter expression: something that can render itself as an OData expression string.
/// </summary>
/// <remarks>Port of <c>Expressions.Operand</c> (a top-level interface here).</remarks>
public interface IOperand
{
    /// <summary>Renders the operand without lambda variable prefixes.</summary>
    /// <param name="protocol">The OData protocol.</param>
    /// <returns>The expression string.</returns>
    string GetExpression(IODataProtocol protocol) => GetExpression(protocol, Expressions.NoPrefixes);

    /// <summary>Renders the operand.</summary>
    /// <param name="protocol">The OData protocol.</param>
    /// <param name="prefixes">Lambda variable prefixes in effect, each with the predicate selecting the fields it applies to.</param>
    /// <returns>The expression string.</returns>
    string GetExpression(IODataProtocol protocol, IReadOnlyDictionary<string, Func<IFieldReference, bool>> prefixes);

    /// <summary>The <c>null</c> operand.</summary>
    static IOperand Null => Expressions.NullOperand;
}

/// <summary>An operand that yields a single value.</summary>
public interface IOperandSingle : IOperand
{
}

/// <summary>An operand that yields multiple values (a collection).</summary>
public interface IOperandMultiple : IOperand
{
}

/// <summary>A filter expression with an operator and operands.</summary>
public interface IFilterExpression : IOperand
{
    /// <summary>The operator or function name.</summary>
    string FilterOperator { get; }

    /// <summary>The operands.</summary>
    IReadOnlyList<IOperand> Operands { get; }
}

/// <summary>Factory methods for filter expression building blocks.</summary>
public static class Expressions
{
    internal static Prefixes NoPrefixes { get; } = new Dictionary<string, Func<IFieldReference, bool>>();

    internal static IOperand NullOperand { get; } = new DelegateOperand((_, _) => "null");

    private sealed class DefaultFilterExpression(
        Func<string, IReadOnlyList<string>, string> format,
        string @operator,
        IReadOnlyList<IOperand> operands) : IFilterExpression
    {
        public string FilterOperator => @operator;

        public IReadOnlyList<IOperand> Operands => operands;

        public string GetExpression(IODataProtocol protocol, Prefixes prefixes) =>
            format(@operator, operands.Select(o => o.GetExpression(protocol, prefixes)).ToList());
    }

    private sealed class DelegateOperand(Func<IODataProtocol, Prefixes, string> expression) : IOperand
    {
        public string GetExpression(IODataProtocol protocol, Prefixes prefixes) => expression(protocol, prefixes);
    }

    internal static IFilterExpression CreateOperatorPrefix(string @operator, IOperand operand) =>
        new DefaultFilterExpression((op, p) => $"({op} {p[0]})", @operator, [operand]);

    internal static IFilterExpression CreateFunctionPrefix(string @operator) =>
        new DefaultFilterExpression((op, _) => $"{op}()", @operator, []);

    internal static IFilterExpression CreateFunctionPrefix(string @operator, IOperand operand) =>
        new DefaultFilterExpression((op, p) => $"{op}({p[0]})", @operator, [operand]);

    internal static IFilterExpression CreateFunctionPrefix(string @operator, IOperand operand1, IOperand operand2) =>
        new DefaultFilterExpression((op, p) => $"{op}({p[0]},{p[1]})", @operator, [operand1, operand2]);

    internal static IFilterExpression CreateFunctionPrefix(
        string @operator,
        IOperand operand1,
        IOperand operand2,
        IOperand operand3) =>
        new DefaultFilterExpression((op, p) => $"{op}({p[0]},{p[1]},{p[2]})", @operator, [operand1, operand2, operand3]);

    internal static IFilterExpression CreateFunctionInfix(string @operator, IOperand operand1, IOperand operand2) =>
        new DefaultFilterExpression((op, p) => $"({p[0]} {op} {p[1]})", @operator, [operand1, operand2]);

    internal static IFilterExpression CreateFunctionLambda(string @operator, IOperandMultiple operand1) =>
        new DefaultFilterExpression((op, p) => $"{p[0]}/{op}()", @operator, [operand1]);

    internal static IFilterExpression CreateFunctionLambda(
        string @operator,
        IOperandMultiple operand1,
        IOperand operand2,
        Func<IFieldReference, bool> lambdaFieldPredicate)
    {
        var operandLambda = new DelegateOperand((protocol, prefixes) =>
        {
            var lambdaFieldPrefix = ((char)('a' + prefixes.Count)).ToString();
            var prefixed = new OrderedDictionary<string, Func<IFieldReference, bool>>
            {
                [lambdaFieldPrefix] = lambdaFieldPredicate, // prepend prefix
            };
            foreach (var (key, value) in prefixes)
            {
                prefixed[key] = value;
            }
            return lambdaFieldPrefix + ":" + operand2.GetExpression(protocol, prefixed);
        });
        return new DefaultFilterExpression((op, p) => $"{p[0]}/{op}({p[1]})", @operator, [operand1, operandLambda]);
    }

    /// <summary>Converts a .NET value to a filter expression operand.</summary>
    /// <remarks>
    /// Supported: <see langword="null"/>, <see cref="IOperandSingle"/>, <see cref="string"/>, <see cref="bool"/>, numbers,
    /// <see cref="TimeSpan"/>, <see cref="DateTime"/>, <see cref="DateTimeOffset"/>, <see cref="DateOnly"/>,
    /// <see cref="TimeOnly"/>, <see cref="Guid"/> and <c>byte[]</c>.
    /// </remarks>
    /// <param name="value">The value.</param>
    /// <returns>The operand.</returns>
    /// <exception cref="ArgumentException">The value type is not supported.</exception>
    public static IOperandSingle CreateOperand(object? value) => value switch
    {
        null => (IOperandSingle)ValueNullOperand.Instance,
        IOperandSingle single => single,
        string s => IValueString.Literal(s),
        bool b => IValueBoolean.Literal(b),
        sbyte or byte or short or int or long or float or double or decimal or BigInteger => ValueNumericFactory.LiteralBoxed(value),
        TimeSpan d => IValueDuration.Literal(d),
        DateTime dt => IValueDateTime.Literal(dt),
        DateTimeOffset dto => IValueDateTimeOffset.Literal(dto),
        DateOnly date => IValueDate.Literal(date),
        TimeOnly time => IValueTimeOfDay.Literal(time),
        Guid g => IValueGuid.Literal(g),
        byte[] bytes => IValueBinary.Literal(bytes),
        _ => throw new ArgumentException("Unable to create filter expression for value " + value),
    };

    private sealed class ValueNullOperand : IOperandSingle
    {
        internal static ValueNullOperand Instance { get; } = new();

        public string GetExpression(IODataProtocol protocol, Prefixes prefixes) => "null";
    }
}
