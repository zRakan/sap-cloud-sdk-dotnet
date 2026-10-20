using Prefixes = System.Collections.Generic.IReadOnlyDictionary<string, System.Func<Sap.Cloud.Sdk.DataModel.OData.Client.Expression.IFieldReference, bool>>;

namespace Sap.Cloud.Sdk.DataModel.OData.Client.Expression;

internal sealed class ValueBinaryImpl(Func<IODataProtocol, Prefixes, string> expression) : IValueBinary
{
    public string GetExpression(IODataProtocol protocol, Prefixes prefixes) => expression(protocol, prefixes);
}

internal sealed class ValueBooleanImpl(Func<IODataProtocol, Prefixes, string> expression) : IValueBoolean
{
    public string GetExpression(IODataProtocol protocol, Prefixes prefixes) => expression(protocol, prefixes);
}

internal sealed class ValueArrayImpl(Func<IODataProtocol, Prefixes, string> expression) : IValueArray
{
    public string GetExpression(IODataProtocol protocol, Prefixes prefixes) => expression(protocol, prefixes);
}

internal sealed class ValueDateImpl(Func<IODataProtocol, Prefixes, string> expression) : IValueDate
{
    public string GetExpression(IODataProtocol protocol, Prefixes prefixes) => expression(protocol, prefixes);
}

internal sealed class ValueDateTimeImpl(Func<IODataProtocol, Prefixes, string> expression) : IValueDateTime
{
    public string GetExpression(IODataProtocol protocol, Prefixes prefixes) => expression(protocol, prefixes);
}

internal sealed class ValueDateTimeOffsetImpl(Func<IODataProtocol, Prefixes, string> expression) : IValueDateTimeOffset
{
    public string GetExpression(IODataProtocol protocol, Prefixes prefixes) => expression(protocol, prefixes);
}

internal sealed class ValueDurationImpl(Func<IODataProtocol, Prefixes, string> expression) : IValueDuration
{
    public string GetExpression(IODataProtocol protocol, Prefixes prefixes) => expression(protocol, prefixes);
}

internal sealed class ValueEnumMemberImpl(Func<IODataProtocol, Prefixes, string> expression) : IValueEnumMember
{
    public string GetExpression(IODataProtocol protocol, Prefixes prefixes) => expression(protocol, prefixes);
}

internal sealed class ValueGuidImpl(Func<IODataProtocol, Prefixes, string> expression) : IValueGuid
{
    public string GetExpression(IODataProtocol protocol, Prefixes prefixes) => expression(protocol, prefixes);
}

internal sealed class ValueNumericImpl(Func<IODataProtocol, Prefixes, string> expression) : IValueNumeric
{
    public string GetExpression(IODataProtocol protocol, Prefixes prefixes) => expression(protocol, prefixes);
}

internal sealed class ValueStringImpl(Func<IODataProtocol, Prefixes, string> expression) : IValueString
{
    public string GetExpression(IODataProtocol protocol, Prefixes prefixes) => expression(protocol, prefixes);
}

internal sealed class ValueTimeOfDayImpl(Func<IODataProtocol, Prefixes, string> expression) : IValueTimeOfDay
{
    public string GetExpression(IODataProtocol protocol, Prefixes prefixes) => expression(protocol, prefixes);
}

/// <summary>Wraps operands as typed values.</summary>
internal static class Values
{
    internal static IValueBinary AsBinary(IOperand operand) => new ValueBinaryImpl(operand.GetExpression);

    internal static IValueBoolean AsBoolean(IOperand operand) => new ValueBooleanImpl(operand.GetExpression);

    internal static IValueArray AsArray(IOperand operand) => new ValueArrayImpl(operand.GetExpression);

    internal static IValueDate AsDate(IOperand operand) => new ValueDateImpl(operand.GetExpression);

    internal static IValueDateTime AsDateTime(IOperand operand) => new ValueDateTimeImpl(operand.GetExpression);

    internal static IValueDateTimeOffset AsDateTimeOffset(IOperand operand) => new ValueDateTimeOffsetImpl(operand.GetExpression);

    internal static IValueDuration AsDuration(IOperand operand) => new ValueDurationImpl(operand.GetExpression);

    internal static IValueEnumMember AsEnum(IOperand operand) => new ValueEnumMemberImpl(operand.GetExpression);

    internal static IValueGuid AsGuid(IOperand operand) => new ValueGuidImpl(operand.GetExpression);

    internal static IValueNumeric AsNumeric(IOperand operand) => new ValueNumericImpl(operand.GetExpression);

    internal static IValueString AsString(IOperand operand) => new ValueStringImpl(operand.GetExpression);

    internal static IValueTimeOfDay AsTimeOfDay(IOperand operand) => new ValueTimeOfDayImpl(operand.GetExpression);

}
